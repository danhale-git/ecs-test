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
public class MapBufferChangeSystem : ComponentSystem
{
	EntityManager entityManager;

	int cubeSize;

	ArchetypeChunkEntityType 					entityType;
	ArchetypeChunkComponentType<MapSquare>		mapSquareType;
	ArchetypeChunkBufferType<Block> 			blocksType;
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
			All  	= new ComponentType[] { typeof(MapSquare), typeof(Tags.BufferChanged) }
		};
	}

	protected override void OnUpdate()
	{
		entityType 		= GetArchetypeChunkEntityType();
		mapSquareType	= GetArchetypeChunkComponentType<MapSquare>();

		blocksType 		= GetArchetypeChunkBufferType<Block>();
        heightmapType 	= GetArchetypeChunkBufferType<Topology>();

		NativeArray<ArchetypeChunk> chunks = entityManager.CreateArchetypeChunkArray(
			mapSquareQuery,
			Allocator.TempJob
			);

		if(chunks.Length == 0) chunks.Dispose();
		else UpdateBuffers(chunks);
	}

	void UpdateBuffers(NativeArray<ArchetypeChunk> chunks)
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

				float sliceLength = math.pow(cubeSize, 2);

				float bottomSliceCount 	= blockBuffer[0].localPosition.y - mapSquare.bottomBlockBuffer;
				float topSliceCount 	= mapSquare.topBlockBuffer - blockBuffer[blockBuffer.Length-1].localPosition.y;

				NativeArray<Block> oldBlocks = new NativeArray<Block>(blockBuffer.Length, Allocator.TempJob);
				oldBlocks.CopyFrom(blockBuffer.AsNativeArray());

				DynamicBuffer<Block> newBuffer = commandBuffer.SetBuffer<Block>(entity);
				newBuffer.ResizeUninitialized(mapSquare.blockGenerationArrayLength);

				int bottomOffset 	= (int)	(bottomSliceCount	* sliceLength);
				int topOffset		= (int)	(topSliceCount		* sliceLength);
				int topStart		= 		(bottomOffset + oldBlocks.Length);

				NativeArray<Block> prepend = GetBlocks(mapSquare, heightmap, bottomOffset);
				NativeArray<Block> append = GetBlocks(mapSquare, heightmap, topOffset, topStart);

				for(int i = 0; i < bottomOffset; i++)
					newBuffer[i] 				= prepend[i];//GetBlock(i, mapSquare, heightmap);

				for(int i = 0; i < oldBlocks.Length; i++)
					newBuffer[i+bottomOffset] 	= oldBlocks[i];

				for(int i = 0; i < topOffset; i++)
					newBuffer[i + topStart] 	= append[i];//GetBlock(index, mapSquare, heightmap);

				commandBuffer.RemoveComponent<Tags.BufferChanged>(entity);

				prepend.Dispose();
				append.Dispose(); 
				oldBlocks.Dispose();
			}
		}
		
		commandBuffer.Playback(entityManager);
		commandBuffer.Dispose();

		chunks.Dispose();
	}

	NativeArray<Block> GetBlocks(MapSquare mapSquare, DynamicBuffer<Topology> heightMap, int arrayLength, int offset = 0)
	{
		//	Block data for all cubes in the map square
		var blocks = new NativeArray<Block>(arrayLength, Allocator.TempJob);

		BlocksJob job = new BlocksJob{
			blocks = blocks,
			mapSquare = mapSquare,
			heightMap = heightMap,
			cubeSize = cubeSize,
			util = new JobUtil(),
			offset = offset
		};
		
		job.Schedule(arrayLength, 1).Complete(); 

		return blocks;
	}
}
