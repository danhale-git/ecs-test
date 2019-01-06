using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using MyComponents;

[UpdateAfter(typeof(BlockBufferSystem))]
public class BlockSystem : ComponentSystem
{
	EntityManager entityManager;

	int cubeSize;

	ArchetypeChunkEntityType 			entityType;
	ArchetypeChunkBufferType<Block> 	blocksType;
	ArchetypeChunkBufferType<Topology> 	heightmapType;

	EntityArchetypeQuery mapSquareQuery;

	protected override void OnCreateManager()
	{
		entityManager = World.Active.GetOrCreateManager<EntityManager>();
		cubeSize = TerrainSettings.cubeSize;

		mapSquareQuery = new EntityArchetypeQuery
		{
			Any 	= Array.Empty<ComponentType>(),
			None  	= new ComponentType[] { typeof(Tags.OuterBuffer) },
			All  	= new ComponentType[] { typeof(MapSquare), typeof(Tags.GenerateBlocks) }
		};
	}

	protected override void OnUpdate()
	{
		entityType 		= GetArchetypeChunkEntityType();

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
			BufferAccessor<Block> blockAccessor 		= chunk.GetBufferAccessor(blocksType);
            BufferAccessor<Topology> heightmapAccessor 	= chunk.GetBufferAccessor(heightmapType);
			
			for(int e = 0; e < entities.Length; e++)
			{
				Entity entity 						= entities[e];
				DynamicBuffer<Block> blockBuffer 	= blockAccessor[e];
                DynamicBuffer<Topology> heightmap		= heightmapAccessor[e];

				MapSquare mapSquare = entityManager.GetComponentData<MapSquare>(entity);

				commandBuffer.SetComponent<MapSquare>(entity, mapSquare);

				//	Resize buffer to size of (blocks in a cube) * (number of cubes)
				blockBuffer.ResizeUninitialized(mapSquare.blockGenerationArrayLength);

				//	Generate block data from height map
				NativeArray<Block> blocks = GetBlocks(
					commandBuffer,
					entities[e],
					1,
					mapSquare,
					heightmap.ToNativeArray()
					);

				//	Fill buffer
				for(int b = 0; b < blocks.Length; b++)
					blockBuffer [b] = blocks[b];

				//	Draw mesh next
				commandBuffer.RemoveComponent<Tags.GenerateBlocks>(entity);
                commandBuffer.AddComponent(entity, new Tags.DrawMesh());

				blocks.Dispose();
			}
		}
		
		commandBuffer.Playback(entityManager);
		commandBuffer.Dispose();

		chunks.Dispose();
	}

	NativeArray<Block> GetBlocks(EntityCommandBuffer commandBuffer, Entity entity, int batchSize, MapSquare mapSquare, NativeArray<Topology> heightMap)
	{
		//	Block data for all cubes in the map square
		var blocks = new NativeArray<Block>(mapSquare.blockGenerationArrayLength, Allocator.TempJob);

		//CustomDebugTools.SetMapSquareHighlight(entity, cubeSize, new Color(1, 1, 1, 0.1f), mapSquare.topBlock, mapSquare.bottomBlock);
		CustomDebugTools.SetMapSquareHighlight(entity, cubeSize-1, new Color(1, 1, 1, 0.2f), mapSquare.topBlockBuffer, mapSquare.bottomBlockBuffer);

		//CustomDebugTools.MapSquareBufferDebug(entity);

		/*for(int y = 0; y < mapSquare.height; y++)
			for(int z = 0; z < cubeSize; z++)
				for(int x = 0; x < cubeSize; x++)
				{
					blocks[Util.Flatten2(x, y, z, cubeSize)] = new Block{
					type = 1,
					localPosition = float3.zero//Util.Unflatten2(i, cubeSize)
					};
				} */

		/*for(int i = 0; i < blocks.Length; i++)
			blocks[i] = new Block{
				type = 1,
				localPosition = float3.zero//Util.Unflatten2(i, cubeSize)
			};  */

		BlocksJob job = new BlocksJob{
			blocks = blocks,
			mapSquare = mapSquare,
			heightMap = heightMap,
			cubeSize = cubeSize,
			util = new JobUtil()
		};
		
		job.Schedule(mapSquare.blockGenerationArrayLength, batchSize).Complete(); 

		return blocks;
	}
}
