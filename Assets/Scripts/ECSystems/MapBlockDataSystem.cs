using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using MyComponents;

//	Generate 3D block data from 2D terrain data
[UpdateAfter(typeof(MapOuterBufferSystem))]
public class MapBlockDataSystem : ComponentSystem
{
	EntityManager entityManager;

	int cubeSize;

	ArchetypeChunkEntityType 				entityType;
	ArchetypeChunkComponentType<MapSquare>	mapSquareType;
	ArchetypeChunkBufferType<Block> 		blocksType;
	ArchetypeChunkBufferType<Topology> 		heightmapType;
	

	EntityArchetypeQuery mapSquareQuery;

	protected override void OnCreateManager()
	{
		entityManager = World.Active.GetOrCreateManager<EntityManager>();
		cubeSize = TerrainSettings.cubeSize;

		mapSquareQuery = new EntityArchetypeQuery
		{
			Any 	= Array.Empty<ComponentType>(),
			None  	= new ComponentType[] { typeof(Tags.EdgeBuffer), typeof(Tags.OuterBuffer) },
			All  	= new ComponentType[] { typeof(MapSquare), typeof(Tags.GenerateBlocks) }
		};
	}

	protected override void OnUpdate()
	{
		entityType 		= GetArchetypeChunkEntityType();
		mapSquareType	= GetArchetypeChunkComponentType<MapSquare>();

		blocksType 		= GetArchetypeChunkBufferType<Block>();
        heightmapType = GetArchetypeChunkBufferType<Topology>();

		NativeArray<ArchetypeChunk> chunks = entityManager.CreateArchetypeChunkArray(
			mapSquareQuery,
			Allocator.TempJob
			);

		if(chunks.Length == 0) chunks.Dispose();
		else GenerateCubes(chunks);
	}

	void GenerateCubes(NativeArray<ArchetypeChunk> chunks)
	{
		EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);

		for(int c = 0; c < chunks.Length; c++)
		{
			ArchetypeChunk chunk = chunks[c];

			NativeArray<Entity> entities 				= chunk.GetNativeArray(entityType);
			NativeArray<MapSquare> mapSquares			= chunk.GetNativeArray(mapSquareType);
			BufferAccessor<Block> blockAccessor 		= chunk.GetBufferAccessor(blocksType);
            BufferAccessor<Topology> heightmapAccessor 	= chunk.GetBufferAccessor(heightmapType);
			
			for(int e = 0; e < entities.Length; e++)
			{
				Entity entity 						= entities[e];
				DynamicBuffer<Block> blockBuffer 	= blockAccessor[e];
                DynamicBuffer<Topology> heightmap		= heightmapAccessor[e];

				MapSquare mapSquare = entityManager.GetComponentData<MapSquare>(entity);

				//	Resize buffer to size of (blocks in a cube) * (number of cubes)
				blockBuffer.ResizeUninitialized(mapSquare.blockGenerationArrayLength);

				//	Generate block data from height map
				NativeArray<Block> blocks = GetBlocks(
					entities[e],
					mapSquares[e],
					heightmap.ToNativeArray()
					);

				//	Fill buffer
				for(int b = 0; b < blocks.Length; b++)
					blockBuffer [b] = blocks[b];

				//	Set slopes next
				commandBuffer.RemoveComponent<Tags.GenerateBlocks>(entity);
                commandBuffer.AddComponent(entity, new Tags.SetSlopes());

				blocks.Dispose();
			}
		}
		
		commandBuffer.Playback(entityManager);
		commandBuffer.Dispose();

		chunks.Dispose();
	}

	NativeArray<Block> GetBlocks(Entity entity, MapSquare mapSquare, NativeArray<Topology> heightMap)
	{
		//	Block data for all cubes in the map square
		var blocks = new NativeArray<Block>(mapSquare.blockGenerationArrayLength, Allocator.TempJob);

		BlocksJob job = new BlocksJob{
			blocks = blocks,
			mapSquare = mapSquare,
			heightMap = heightMap,
			cubeSize = cubeSize,
			util = new JobUtil()
		};
		
		job.Schedule(mapSquare.blockGenerationArrayLength, 1).Complete(); 

		return blocks;
	}
}
