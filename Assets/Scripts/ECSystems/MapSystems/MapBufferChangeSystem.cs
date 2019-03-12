using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using MyComponents;

//	Increase size of block data array and fill gaps
[UpdateAfter(typeof(MapVerticalDrawBufferSystem))]
public class MapBufferChangeSystem : ComponentSystem
{
	EntityManager entityManager;

	int squareWidth;

	ComponentGroup bufferChangedGroup;

	protected override void OnCreateManager()
	{
		entityManager = World.Active.GetOrCreateManager<EntityManager>();

		squareWidth = TerrainSettings.mapSquareWidth;

		EntityArchetypeQuery mapSquareQuery = new EntityArchetypeQuery{
			None  	= new ComponentType[] { typeof(Tags.EdgeBuffer), typeof(Tags.OuterBuffer) },
			All  	= new ComponentType[] { typeof(MapSquare), typeof(Tags.BufferChanged) }
		};
		bufferChangedGroup = GetComponentGroup(mapSquareQuery);
	}

	protected override void OnUpdate()
	{
		EntityCommandBuffer 		commandBuffer 	= new EntityCommandBuffer(Allocator.Temp);
		NativeArray<ArchetypeChunk> chunks 			= bufferChangedGroup.CreateArchetypeChunkArray(Allocator.TempJob);

		ArchetypeChunkEntityType 				entityType 		= GetArchetypeChunkEntityType();
		ArchetypeChunkComponentType<MapSquare> 	mapSquareType	= GetArchetypeChunkComponentType<MapSquare>(true);
		ArchetypeChunkBufferType<Block> 		blocksType 		= GetArchetypeChunkBufferType<Block>();
        ArchetypeChunkBufferType<Topology> 		heightmapType 	= GetArchetypeChunkBufferType<Topology>(true);

		for(int c = 0; c < chunks.Length; c++)
		{
			ArchetypeChunk chunk = chunks[c];

			NativeArray<Entity> 		entities			= chunk.GetNativeArray(entityType);
			NativeArray<MapSquare> 		mapSquares			= chunk.GetNativeArray(mapSquareType);
			BufferAccessor<Block> 		blockAccessor 		= chunk.GetBufferAccessor(blocksType);
            BufferAccessor<Topology> 	heightmapAccessor 	= chunk.GetBufferAccessor(heightmapType);
			
			for(int e = 0; e < entities.Length; e++)
			{
				Entity 					entity		= entities[e];
				MapSquare 				mapSquare	= mapSquares[e];
				DynamicBuffer<Block> 	blockBuffer = blockAccessor[e];
                DynamicBuffer<Topology> heightmap	= heightmapAccessor[e];

				//	Length of one horizontal slice from the 3D array when flattened
				float sliceLength = math.pow(squareWidth, 2);

				//	Current array of blocks
				NativeArray<Block> oldBlocks = new NativeArray<Block>(blockBuffer.Length, Allocator.TempJob);
				oldBlocks.CopyFrom(blockBuffer.AsNativeArray());

				//	Reset map square entity's block buffer at new length
				DynamicBuffer<Block> newBuffer = commandBuffer.SetBuffer<Block>(entity);
				newBuffer.ResizeUninitialized(mapSquare.blockDataArrayLength);

				//	Number of additional array slices added at top and bottom
				float bottomSliceCount 	= blockBuffer[0].localPosition.y - mapSquare.bottomBlockBuffer;
				float topSliceCount 	= mapSquare.topBlockBuffer - blockBuffer[blockBuffer.Length-1].localPosition.y;

				//	Indices at which bottom and top additions start and finish
				int	bottomStart		= 0;
				int bottomOffset 	= (int)	(bottomSliceCount * sliceLength);
				int topStart		= 		(bottomOffset + oldBlocks.Length);
				int topOffset		= (int)	(topSliceCount	* sliceLength);

				//	Block data for bottom and top additions
				NativeArray<Block> prepend = GetBlocks(mapSquare, heightmap, bottomOffset, bottomStart);
				NativeArray<Block> append = GetBlocks(mapSquare, heightmap, topOffset, topStart);

				//	Add blocks to bottom of array
				for(int i = 0; i < bottomOffset; i++)
					newBuffer[i] 				= prepend[i];

				//	Add existing block data
				for(int i = 0; i < oldBlocks.Length; i++)
					newBuffer[i+bottomOffset] 	= oldBlocks[i];

				//	Add blocks to top of array
				for(int i = 0; i < topOffset; i++)
					newBuffer[i + topStart] 	= append[i];

				commandBuffer.RemoveComponent<Tags.BufferChanged>(entity);

				prepend.Dispose();
				oldBlocks.Dispose();
				append.Dispose(); 
			}
		}
		
		commandBuffer.Playback(entityManager);
		commandBuffer.Dispose();

		chunks.Dispose();
	}

	NativeArray<Block> GetBlocks(MapSquare mapSquare, DynamicBuffer<Topology> heightMap, int arrayLength, int offset)
	{
		//	Block data for all cubes in the map square
		var blocks = new NativeArray<Block>(arrayLength, Allocator.TempJob);

		BlocksJob job = new BlocksJob{
			blocks 		= blocks,
			mapSquare 	= mapSquare,
			heightMap 	= heightMap,
			squareWidth = squareWidth,
			util 		= new JobUtil(),
			offset 		= offset
		};
		
		job.Schedule(arrayLength, 1).Complete(); 

		return blocks;
	}
}
