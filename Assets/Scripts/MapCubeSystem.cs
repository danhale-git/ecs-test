using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using System;
using MyComponents;

[UpdateAfter(typeof(MapSquareSystem))]
public class MapCubeSystem : ComponentSystem
{
	PlayerController player;

	EntityManager entityManager;

	//	Chunk data
	EntityArchetype mapChunkArchetype;

	static int cubeSize;
	int viewDistance;

	ArchetypeChunkEntityType entityType;
	ArchetypeChunkComponentType<MapSquare> squareType;
	EntityArchetypeQuery mapSquareQuery;

	protected override void OnCreateManager()
	{
		cubeSize = TerrainSettings.cubeSize;
		viewDistance = TerrainSettings.viewDistance;

		player = GameObject.FindObjectOfType<PlayerController>();

		entityManager = World.Active.GetOrCreateManager<EntityManager>();

		mapChunkArchetype = entityManager.CreateArchetype(
				ComponentType.Create<MapCube>(),
				ComponentType.Create<Block>(),
				ComponentType.Create<CREATE>()
			);

		//	Map squares without chunks
		mapSquareQuery = new EntityArchetypeQuery
		{
			Any = Array.Empty<ComponentType>(),
			None = new ComponentType [] { },
			All = new ComponentType [] { typeof(MapSquare), typeof(Tags.CreateCubes) }
		};
	}

	protected override void OnUpdate()
	{
		entityType = GetArchetypeChunkEntityType();
		squareType = GetArchetypeChunkComponentType<MapSquare>();

		NativeArray<ArchetypeChunk> dataChunks = entityManager.CreateArchetypeChunkArray(mapSquareQuery, Allocator.TempJob);

		if(dataChunks.Length == 0)
			dataChunks.Dispose();
		else
			ProcessChunks(dataChunks);
	}


	void ProcessChunks(NativeArray<ArchetypeChunk> dataChunks)
	{
		EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);

		for(int d = 0; d < dataChunks.Length; d++)
		{
			var dataChunk = dataChunks[d];
			var entities = dataChunk.GetNativeArray(entityType);
			var squares = dataChunk.GetNativeArray(squareType);

			for(int e = 0; e < entities.Length; e++)
			{
				var squareEntity = entities[e];
				var squareWorldPosition = squares[e].worldPosition;


				DynamicBuffer<Height> heightBuffer = entityManager.GetBuffer<Height>(squareEntity);

				float3 cPos = new float3(squareWorldPosition.x, 0, squareWorldPosition.y);

				CreateChunk(cPos, squareEntity, commandBuffer);

				commandBuffer.RemoveComponent<Tags.CreateCubes>(squareEntity);
			}

			commandBuffer.Playback(entityManager);
			commandBuffer.Dispose();

			dataChunks.Dispose();
		}
	}

	//	Create one chunk
	MapCube CreateChunk(Vector3 position, Entity parentSquare, EntityCommandBuffer commandBuffer)
	{
		//	Chunk is at map edge
		bool edge = !entityManager.HasComponent(parentSquare, typeof(Tags.DrawMesh));

		Color debugColor = edge ? new Color(1f,.3f,.3f,0.2f) : new Color(1f,1f,1f,0.2f);
		CustomDebugTools.WireCubeChunk(position, cubeSize - 1, debugColor, false);

		//	Create chunk entity
		commandBuffer.CreateEntity(mapChunkArchetype);
		MapCube chunkComponent = new MapCube
		{
			worldPosition = position,
			parentMapSquare = parentSquare
		};
		commandBuffer.SetComponent<MapCube>(chunkComponent);

		//	Chunk is at map edge
		if(!edge)
			commandBuffer.AddComponent<Tags.DrawMesh>(new Tags.DrawMesh());
		
		commandBuffer.AddComponent<Tags.GenerateBlocks>(new Tags.GenerateBlocks());

		return chunkComponent;
	}

	public static Entity[] GetAdjacentSquares(Vector3 position)
	{
		float3 pos = position;
		Entity[] adjacent = new Entity[6];
		
		/*adjacent[0] = chunks[pos +(new float3( 1,	0, 0) * chunkSize)];


		if(!chunks.ContainsKey(pos +(new float3(-1,	0, 0) * chunkSize)))
		{
			Debug.Log("failed at position "+pos);
			CustomDebugTools.WireCubeChunk(pos, 4, Color.green, false);
		}


		adjacent[1] = chunks[pos +(new float3(-1,	0, 0) * chunkSize)];

		//adjacent[2] = map[pos +(new float3( 0,	1, 0) * chunkSize)];
		//adjacent[3] = map[pos +(new float3( 0,-1, 0) * chunkSize)];
		adjacent[2] = chunks[pos +(new float3( 0,	0, 0) * chunkSize)];
		adjacent[3] = chunks[pos +(new float3( 0, 0, 0) * chunkSize)];

		adjacent[4] = chunks[pos +(new float3( 0,	0, 1) * chunkSize)];
		adjacent[5] = chunks[pos +(new float3( 0,	0,-1) * chunkSize)];*/

		return adjacent;
	}
}