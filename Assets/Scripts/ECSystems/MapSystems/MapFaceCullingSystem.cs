using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Rendering;
using MyComponents;

using UnityEngine;
using UnityEditor;

//	Generate 3D mesh from block data
[UpdateAfter(typeof(MapSlopeSystem))]
[UpdateAfter(typeof(MapBufferChangeSystem))]
[UpdateBefore(typeof(TransformSystemGroup))]
public class MapFaceCullingSystem : ComponentSystem
{
	//	Parralel job batch size
	int batchSize = 32;

	EntityManager entityManager;

	int squareWidth;

	ComponentGroup meshGroup;

	JobHandle currentJobHandle;
	EntityCommandBuffer currentEntityCommandBuffer;

	protected override void OnCreateManager()
	{
		entityManager = World.Active.GetOrCreateManager<EntityManager>();

		squareWidth = TerrainSettings.mapSquareWidth;

		EntityArchetypeQuery squareQuery = new EntityArchetypeQuery{
			None  	= new ComponentType[] { typeof(Tags.EdgeBuffer), typeof(Tags.OuterBuffer), typeof(Tags.InnerBuffer) },
			All  	= new ComponentType[] { typeof(MapSquare), typeof(Tags.DrawMesh), typeof(AdjacentSquares) }
		};
		meshGroup = GetComponentGroup(squareQuery);
	}

	//	Query for meshes that need drawing
	protected override void OnUpdate()
	{
		EntityCommandBuffer 		commandBuffer 	= new EntityCommandBuffer(Allocator.TempJob);
		NativeArray<ArchetypeChunk> chunks 			= meshGroup.CreateArchetypeChunkArray(Allocator.TempJob);

		ArchetypeChunkEntityType 						entityType 		= GetArchetypeChunkEntityType();
		ArchetypeChunkComponentType<MapSquare> 			squareType		= GetArchetypeChunkComponentType<MapSquare>(true);
		ArchetypeChunkComponentType<Translation> 		positionType	= GetArchetypeChunkComponentType<Translation>(true);
		ArchetypeChunkComponentType<AdjacentSquares>	adjacentType	= GetArchetypeChunkComponentType<AdjacentSquares>(true);
		ArchetypeChunkBufferType<Block> 				blocksType 		= GetArchetypeChunkBufferType<Block>(true);

		for(int c = 0; c < chunks.Length; c++)
		{
			ArchetypeChunk chunk = chunks[c];

			//	Get chunk data
			NativeArray<Entity> 			entities 		= chunk.GetNativeArray(entityType);
			NativeArray<MapSquare>			squares			= chunk.GetNativeArray(squareType);
			NativeArray<Translation>		positions		= chunk.GetNativeArray(positionType);
			NativeArray<AdjacentSquares>	adjacentSquaresArray	= chunk.GetNativeArray(adjacentType);
			BufferAccessor<Block> 			blockAccessor 	= chunk.GetBufferAccessor(blocksType);

			for(int e = 0; e < entities.Length; e++)
			{
				Entity entity = entities[e];
				AdjacentSquares adjacentSquares = adjacentSquaresArray[e];
				DynamicBuffer<Block> blocks = blockAccessor[e];
				MapSquare mapSquare = squares[e];

				NativeArray<float3> directions = Util.CardinalDirections(Allocator.TempJob);

				NativeArray<int> adjacentLowestBlocks = new NativeArray<int>(8, Allocator.TempJob);
				for(int i = 0; i < 8; i++)
					adjacentLowestBlocks[i] = entityManager.GetComponentData<MapSquare>(adjacentSquares[i]).bottomBlockBuffer;
				
				FacesJob job = new FacesJob(){
					commandBuffer = commandBuffer,

					entity = entity,
					mapSquare 		= mapSquare,

					blocks 	= blocks.AsNativeArray(),
					right 	= entityManager.GetBuffer<Block>(adjacentSquares[0]).AsNativeArray(),
					left 	= entityManager.GetBuffer<Block>(adjacentSquares[1]).AsNativeArray(),
					front 	= entityManager.GetBuffer<Block>(adjacentSquares[2]).AsNativeArray(),
					back 	= entityManager.GetBuffer<Block>(adjacentSquares[3]).AsNativeArray(),

					adjacentLowestBlocks = adjacentLowestBlocks,
					
					squareWidth = squareWidth,
					directions 	= directions, 
					util 		= new JobUtil()
				};
				
				job.Schedule().Complete();
			}
		}

		commandBuffer.Playback(entityManager);
		commandBuffer.Dispose();

		chunks.Dispose();
	}
} 