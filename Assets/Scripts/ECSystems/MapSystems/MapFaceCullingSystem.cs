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

	JobHandle runningJobHandle;
	EntityCommandBuffer runningCommandBuffer;

	protected override void OnCreateManager()
	{
		entityManager = World.Active.GetOrCreateManager<EntityManager>();

		squareWidth = TerrainSettings.mapSquareWidth;

		EntityArchetypeQuery squareQuery = new EntityArchetypeQuery{
			None  	= new ComponentType[] { typeof(Tags.EdgeBuffer), typeof(Tags.OuterBuffer), typeof(Tags.InnerBuffer) },
			All  	= new ComponentType[] { typeof(MapSquare), typeof(Tags.DrawMesh), typeof(AdjacentSquares) }
		};
		meshGroup = GetComponentGroup(squareQuery);

		runningJobHandle = new JobHandle();
		runningCommandBuffer = new EntityCommandBuffer(Allocator.TempJob);
	}

	protected override void OnDestroyManager()
	{
		runningCommandBuffer.Dispose();
	}

	protected override void OnUpdate()
	{
		if(!runningJobHandle.IsCompleted)
			return;
		
		JobCompleteAndBufferPlayback();
		ScheduleMoreJobs();
	}

	void ScheduleMoreJobs()
	{
		NativeArray<ArchetypeChunk> chunks 			= meshGroup.CreateArchetypeChunkArray(Allocator.TempJob);

		EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.TempJob);
		JobHandle allHandles		= new JobHandle();
		JobHandle previousHandle	= new JobHandle();

		ArchetypeChunkEntityType 						entityType 		= GetArchetypeChunkEntityType();
		ArchetypeChunkComponentType<MapSquare> 			squareType		= GetArchetypeChunkComponentType<MapSquare>(true);
		ArchetypeChunkComponentType<Translation> 		positionType	= GetArchetypeChunkComponentType<Translation>(true);
		ArchetypeChunkComponentType<AdjacentSquares>	adjacentType	= GetArchetypeChunkComponentType<AdjacentSquares>(true);

		for(int c = 0; c < chunks.Length; c++)
		{
			ArchetypeChunk chunk = chunks[c];

			NativeArray<Entity> 			entities 		= chunk.GetNativeArray(entityType);
			NativeArray<MapSquare>			mapSquares		= chunk.GetNativeArray(squareType);
			NativeArray<Translation>		positions		= chunk.GetNativeArray(positionType);
			NativeArray<AdjacentSquares>	adjacents		= chunk.GetNativeArray(adjacentType);

			for(int e = 0; e < entities.Length; e++)
			{
				Entity entity = entities[e];
				AdjacentSquares adjacentSquares = adjacents[e];

				FacesJob job = new FacesJob(){
					commandBuffer = commandBuffer,

					entity = entity,
					mapSquare = mapSquares[e],

					current 		= new NativeArray<Block>(entityManager.GetBuffer<Block>(entity).AsNativeArray(), Allocator.TempJob),
					rightAdjacent 	= new NativeArray<Block>(entityManager.GetBuffer<Block>(adjacentSquares[0]).AsNativeArray(), Allocator.TempJob),
					leftAdjacent 	= new NativeArray<Block>(entityManager.GetBuffer<Block>(adjacentSquares[1]).AsNativeArray(), Allocator.TempJob),
					frontAdjacent 	= new NativeArray<Block>(entityManager.GetBuffer<Block>(adjacentSquares[2]).AsNativeArray(), Allocator.TempJob),
					backAdjacent 	= new NativeArray<Block>(entityManager.GetBuffer<Block>(adjacentSquares[3]).AsNativeArray(), Allocator.TempJob),

					adjacentLowestBlocks = GetAdjacentLowestBlocks(adjacentSquares),
					
					squareWidth = squareWidth,
					directions 	= Util.CardinalDirections(Allocator.TempJob), 
					util 		= new JobUtil()
				};
				
				JobHandle thisHandle = job.Schedule(previousHandle);
				allHandles = JobHandle.CombineDependencies(thisHandle, allHandles);

				previousHandle = thisHandle;
			}
		}

		runningCommandBuffer = commandBuffer;
		runningJobHandle = allHandles;

		chunks.Dispose();
	}

	NativeArray<int> GetAdjacentLowestBlocks(AdjacentSquares adjacentSquares)
	{
		NativeArray<int> adjacentLowestBlocks = new NativeArray<int>(8, Allocator.TempJob);
		for(int i = 0; i < 8; i++)
			adjacentLowestBlocks[i] = entityManager.GetComponentData<MapSquare>(adjacentSquares[i]).bottomBlockBuffer;

		return adjacentLowestBlocks;
	}

	void JobCompleteAndBufferPlayback()
	{
		runningJobHandle.Complete();

		runningCommandBuffer.Playback(entityManager);
		runningCommandBuffer.Dispose();
	}
} 