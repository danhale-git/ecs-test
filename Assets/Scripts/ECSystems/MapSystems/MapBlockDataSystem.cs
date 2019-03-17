using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using MyComponents;

//	Generate 3D block data from 2D terrain data
[UpdateAfter(typeof(MapVerticalDrawBufferSystem))]
public class MapBlockDataSystem : ComponentSystem
{
	EntityManager entityManager;

	int squareWidth;	

	ComponentGroup generateBlocksGroup;

	protected override void OnCreateManager()
	{
		entityManager = World.Active.GetOrCreateManager<EntityManager>();
		
		squareWidth = TerrainSettings.mapSquareWidth;

		EntityArchetypeQuery mapSquareQuery = new EntityArchetypeQuery{
			None  	= new ComponentType[] { typeof(Tags.EdgeBuffer), typeof(Tags.OuterBuffer), typeof(Tags.SetDrawBuffer) },
			All  	= new ComponentType[] { typeof(MapSquare), typeof(Tags.GenerateBlocks) }
		};
		generateBlocksGroup = GetComponentGroup(mapSquareQuery);
	}

	protected override void OnUpdate()
	{
		EntityCommandBuffer 		commandBuffer 	= new EntityCommandBuffer(Allocator.Temp);
		NativeArray<ArchetypeChunk> chunks 			= generateBlocksGroup.CreateArchetypeChunkArray(Allocator.TempJob);

		ArchetypeChunkEntityType 				entityType 		= GetArchetypeChunkEntityType();
		ArchetypeChunkComponentType<MapSquare> 	mapSquareType	= GetArchetypeChunkComponentType<MapSquare>(true);
		ArchetypeChunkBufferType<Block> 		blocksType 		= GetArchetypeChunkBufferType<Block>();
        ArchetypeChunkBufferType<Topology> 		heightmapType 	= GetArchetypeChunkBufferType<Topology>(true);

		for(int c = 0; c < chunks.Length; c++)
		{
			ArchetypeChunk chunk = chunks[c];

			NativeArray<Entity> 		entities 			= chunk.GetNativeArray(entityType);
			NativeArray<MapSquare> 		mapSquares			= chunk.GetNativeArray(mapSquareType);
			BufferAccessor<Block> 		blockAccessor 		= chunk.GetBufferAccessor(blocksType);
            BufferAccessor<Topology> 	heightmapAccessor 	= chunk.GetBufferAccessor(heightmapType);
			
			for(int e = 0; e < entities.Length; e++)
			{
				Entity entity 						= entities[e];
				MapSquare mapSquare 				= mapSquares[e];
				DynamicBuffer<Block> blockBuffer 	= blockAccessor[e];
                DynamicBuffer<Topology> heightmap	= heightmapAccessor[e];

				//	Resize buffer to size of (blocks in a cube) * (number of cubes)
				blockBuffer.ResizeUninitialized(mapSquare.blockDataArrayLength);

				//	Generate block data from height map
				NativeArray<Block> blocks = GetBlocks(
					mapSquares[e],
					heightmap
				);

				//	Fill buffer
				for(int b = 0; b < blocks.Length; b++)
					blockBuffer [b] = blocks[b];

				//	Set slopes next
				commandBuffer.RemoveComponent<Tags.GenerateBlocks>(entity);

				blocks.Dispose();

				//	Apply loaded changes
				if(entityManager.HasComponent<LoadedChange>(entity))
				{
					DynamicBuffer<LoadedChange> loadedChanges = entityManager.GetBuffer<LoadedChange>(entity);

					for(int i = 0; i < loadedChanges.Length; i++)
					{
						Block block = loadedChanges[i].block;
						int index = Util.BlockIndex(block, mapSquare, squareWidth);
						blockBuffer[index] = block;
					}

					commandBuffer.RemoveComponent<LoadedChange>(entity);
				}
			}
		}
		
		commandBuffer.Playback(entityManager);
		commandBuffer.Dispose();

		chunks.Dispose();
	}

	NativeArray<Block> GetBlocks(MapSquare mapSquare, DynamicBuffer<Topology> heightMap)
	{
		//	Block data for all cubes in the map square
		var blocks = new NativeArray<Block>(mapSquare.blockDataArrayLength, Allocator.TempJob);

		BlocksJob job = new BlocksJob{
			blocks		= blocks,
			mapSquare 	= mapSquare,
			heightMap 	= heightMap,
			squareWidth = squareWidth,
			util 		= new JobUtil()
		};
		
		job.Schedule(mapSquare.blockDataArrayLength, 1).Complete(); 

		return blocks;
	}
}
