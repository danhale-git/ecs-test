using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using System;
using MyComponents;

[UpdateAfter(typeof(MapSquareSystem))]
public class MapChunkSystem : ComponentSystem
{
	PlayerController player;

	EntityManager entityManager;

	//	Chunk data
	EntityArchetype mapChunkArchetype;

	//	All chunks
	public static Dictionary<float3, Entity> chunks;

	static int chunkSize;
	int viewDistance;

	ArchetypeChunkEntityType entityType;
	ArchetypeChunkComponentType<MapSquare> chunkType;
	EntityArchetypeQuery mapSquareQuery;

	protected override void OnCreateManager()
	{
		chunkSize = TerrainSettings.chunkSize;
		viewDistance = TerrainSettings.viewDistance;

		player = GameObject.FindObjectOfType<PlayerController>();

		entityManager = World.Active.GetOrCreateManager<EntityManager>();
		chunks = new Dictionary<float3, Entity>();

		mapChunkArchetype = entityManager.CreateArchetype(
				ComponentType.Create<MapChunk>(),
				ComponentType.Create<Block>(),
				ComponentType.Create<CREATE>()
			);

		//	Map squares without chunks
		mapSquareQuery = new EntityArchetypeQuery
		{
			Any = Array.Empty<ComponentType>(),
			None = new ComponentType [] { typeof(CREATE) },
			All = new ComponentType [] { typeof(MapSquare) }
		};
	}

	protected override void OnUpdate()
	{
		entityType = GetArchetypeChunkEntityType();
		chunkType = GetArchetypeChunkComponentType<MapSquare>();

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
			var squares = dataChunk.GetNativeArray(chunkType);

			int entitiesLength = entities.Length;
            NativeArray<Entity> entityArray = new NativeArray<Entity>(entitiesLength, Allocator.TempJob);
            entityArray.CopyFrom(entities);
            NativeArray<MapSquare> squaresArray = new NativeArray<MapSquare>(squares.Length, Allocator.TempJob);
            squaresArray.CopyFrom(squares);

			for(int e = 0; e < entities.Length; e++)
			{
				//var squareEntity = entities[e];
				//var squareWorldPosition = squares[e].worldPosition;

				var squareEntity = entityArray[e];
				var squareWorldPosition = squaresArray[e].worldPosition;

				DynamicBuffer<Height> heightBuffer = entityManager.GetBuffer<Height>(squareEntity);

				float3 cPos = new float3(squareWorldPosition.x, 0, squareWorldPosition.y);

				CreateChunk(cPos, squareEntity);
				//Debug.Log(entityManager.HasComponent(squareEntity, typeof(MapEdge)));

				entityManager.AddComponent(squareEntity, typeof(CREATE));
			}

			commandBuffer.Playback(entityManager);
			commandBuffer.Dispose();

			entityArray.Dispose();
			squaresArray.Dispose();

			dataChunks.Dispose();
		}
	}

	/*//	Create chunks
	public void GenerateRadius(Vector3 center, int radius)
	{
		center = new Vector3(center.x, 0, center.z);

		//	Generate chunks in square
		for(int x = -radius; x <= radius; x++)
			for(int z = -radius; z <= radius; z++)
			{
				//	Chunk is at the edge of the map
				bool edge=( x == -radius ||
							x ==  radius ||
							z == -radius ||
							z ==  radius   );

				Vector3 offset = new Vector3(x*chunkSize, 0, z*chunkSize);
				CreateChunk(center + offset, edge);
			}

	}*/

	//	Create one chunk
	void CreateChunk(Vector3 position, Entity parentSquare)
	{
		Entity chunkEntity;

		//	Chunk is at map edge
		bool edge = entityManager.HasComponent(parentSquare, typeof(MyTags.DoNotDraw));

		Debug.Log(edge);

		//	Chunk already exists
		if(chunks.TryGetValue(position, out chunkEntity))
		{
			Debug.Log("found chunk"); //	NOT FINDING CHUNKS BECAUSE EXISTING CHUNKS ARE EXCLUDED IN QUERY

			//	Chunk was at the edge but isn't now
			if(!edge && entityManager.HasComponent(chunkEntity, typeof(MyTags.DoNotDraw)) )
			{
				Debug.Log("removing chunk edge");
				//	Remove MapEdge tag
				entityManager.RemoveComponent(chunkEntity, typeof(MyTags.DoNotDraw));
			}
			return;
		}
		else{
			if(position == Vector3.zero)Debug.Log(chunks.Count + " "+ position);
		}

		Color debugColor = edge ? new Color(1f,.3f,.3f,0.2f) : new Color(1f,1f,1f,0.2f);
		CustomDebugTools.WireCubeChunk(position, chunkSize - 1, debugColor, false);

		//	Create chunk entity
		chunkEntity = entityManager.CreateEntity(mapChunkArchetype);
		MapChunk chunkComponent = new MapChunk
		{
			worldPosition = position,
			parentMapSquare = parentSquare
		};
		entityManager.SetComponentData<MapChunk>(chunkEntity, chunkComponent);

		//	Chunk is at map edge
		if(edge) entityManager.AddComponent(chunkEntity, typeof(MyTags.DoNotDraw));

		if(position == Vector3.zero)Debug.Log("adding "+position);
		chunks.Add(position, chunkEntity);
	}

	public static Entity[] GetAdjacentSquares(Vector3 position)
	{
		float3 pos = position;
		Entity[] adjacent = new Entity[6];
		
		adjacent[0] = chunks[pos +(new float3( 1,	0, 0) * chunkSize)];
		adjacent[1] = chunks[pos +(new float3(-1,	0, 0) * chunkSize)];

		//adjacent[2] = map[pos +(new float3( 0,	1, 0) * chunkSize)];
		//adjacent[3] = map[pos +(new float3( 0,-1, 0) * chunkSize)];
		adjacent[2] = chunks[pos +(new float3( 0,	0, 0) * chunkSize)];
		adjacent[3] = chunks[pos +(new float3( 0, 0, 0) * chunkSize)];

		adjacent[4] = chunks[pos +(new float3( 0,	0, 1) * chunkSize)];
		adjacent[5] = chunks[pos +(new float3( 0,	0,-1) * chunkSize)];

		return adjacent;
	}
}