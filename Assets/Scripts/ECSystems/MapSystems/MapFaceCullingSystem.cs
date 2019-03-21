using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Rendering;
using MyComponents;

using System.Collections.Generic;

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

	public JobHandle queuedJobHandle;
	EntityCommandBuffer queuedCommandBuffer;

	protected override void OnCreateManager()
	{
		entityManager = World.Active.GetOrCreateManager<EntityManager>();

		squareWidth = TerrainSettings.mapSquareWidth;

		EntityArchetypeQuery squareQuery = new EntityArchetypeQuery{
			None  	= new ComponentType[] { typeof(Tags.EdgeBuffer), typeof(Tags.OuterBuffer), typeof(Tags.InnerBuffer) },
			All  	= new ComponentType[] { typeof(MapSquare), typeof(Tags.DrawMesh), typeof(AdjacentSquares) }
		};
		meshGroup = GetComponentGroup(squareQuery);

		queuedJobHandle = new JobHandle();
		queuedCommandBuffer = new EntityCommandBuffer(Allocator.TempJob);
	}

	protected override void OnDestroyManager()
	{
	}

	//	Query for meshes that need drawing
	protected override void OnUpdate()
	{
		if(queuedJobHandle.IsCompleted)
		{
			Debug.Log("complete");
			queuedJobHandle.Complete();

			queuedCommandBuffer.Playback(entityManager);
			queuedCommandBuffer.Dispose();
		}
		else
		{
			Debug.Log("running");
			return;
		}

		EntityCommandBuffer 		commandBuffer 	= new EntityCommandBuffer(Allocator.TempJob);
		JobHandle					allHandles		= new JobHandle();
		JobHandle					previousHandle		= new JobHandle();
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
				MapSquare mapSquare = squares[e];

				NativeArray<float3> directions = Util.CardinalDirections(Allocator.TempJob);

				NativeArray<int> adjacentLowestBlocks = new NativeArray<int>(8, Allocator.TempJob);
				for(int i = 0; i < 8; i++)
					adjacentLowestBlocks[i] = entityManager.GetComponentData<MapSquare>(adjacentSquares[i]).bottomBlockBuffer;


				DynamicBuffer<Block> blocksBuffer 	= blockAccessor[e];
				DynamicBuffer<Block> rightBuffer 	= entityManager.GetBuffer<Block>(adjacentSquares[0]);
				DynamicBuffer<Block> leftBuffer 	= entityManager.GetBuffer<Block>(adjacentSquares[1]);
				DynamicBuffer<Block> frontBuffer 	= entityManager.GetBuffer<Block>(adjacentSquares[2]);
				DynamicBuffer<Block> backBuffer 	= entityManager.GetBuffer<Block>(adjacentSquares[3]);

				NativeArray<Block> blocksArray = new NativeArray<Block>(blocksBuffer.Length, Allocator.TempJob);
				blocksArray.CopyFrom(blocksBuffer.AsNativeArray());
				NativeArray<Block> rightArray = new NativeArray<Block>(rightBuffer.Length, Allocator.TempJob);
				rightArray.CopyFrom(rightBuffer.AsNativeArray());
				NativeArray<Block> leftArray = new NativeArray<Block>(leftBuffer.Length, Allocator.TempJob);
				leftArray.CopyFrom(leftBuffer.AsNativeArray());
				NativeArray<Block> frontArray = new NativeArray<Block>(frontBuffer.Length, Allocator.TempJob);
				frontArray.CopyFrom(frontBuffer.AsNativeArray());
				NativeArray<Block> backArray = new NativeArray<Block>(backBuffer.Length, Allocator.TempJob);
				backArray.CopyFrom(backBuffer.AsNativeArray());
				
				FacesJob job = new FacesJob(){
					commandBuffer = commandBuffer,

					entity = entity,
					mapSquare 		= mapSquare,

					blocks 	= blocksArray,
					right 	= rightArray,
					left 	= leftArray,
					front 	= frontArray,
					back 	= backArray,

					adjacentLowestBlocks = adjacentLowestBlocks,
					
					squareWidth = squareWidth,
					directions 	= directions, 
					util 		= new JobUtil()
				};
				
				JobHandle thisHandle = job.Schedule(previousHandle);
				allHandles = JobHandle.CombineDependencies(thisHandle, allHandles);

				previousHandle = thisHandle;
			}
		}

		//commandBuffer.Playback(entityManager);
		//commandBuffer.Dispose();

		queuedCommandBuffer = commandBuffer;
		queuedJobHandle = allHandles;

		chunks.Dispose();
	}
} 