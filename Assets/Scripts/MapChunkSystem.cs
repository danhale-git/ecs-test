using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Unity.Jobs;
using Unity.Collections;

[UpdateAfter(typeof(MapSquareSystem))]
public class MapChunkSystem : ComponentSystem
{
	PlayerController player;

	EntityManager entityManager;

	//	Chunk data
	EntityArchetype mapChunkArchetype;
	EntityArchetype mapSquareArchetype;

	//	All chunks
	public static Dictionary<float3, Entity> chunks;

	static int chunkSize;
	int viewDistance;

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
	}


	//	Continuous generation
	float timer = 1.5f;
	protected override void OnUpdate()
	{
		//	Timer
		if(Time.fixedTime - timer > 1)
		{
			timer = Time.fixedTime;

			//	Generate map in radius around player
			GenerateRadius(
				Util.VoxelOwner(player.transform.position, chunkSize),
				viewDistance);
		}
	}

	//	Create chunks
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

	}

	//	Create one chunk
	void CreateChunk(Vector3 position, bool edge)
	{
		Entity chunkEntity;

		//	Chunk already exists
		if(chunks.TryGetValue(position, out chunkEntity))
		{
			//	Chunk was at the edge but isn't now
			if(!edge && entityManager.HasComponent(chunkEntity, typeof(MapEdge)) )
			{
				//	Remove MapEdge tag
				entityManager.RemoveComponent(chunkEntity, typeof(MapEdge));
			}
			return;
		}

		CustomDebugTools.WireCubeChunk(position, chunkSize - 1, new Color(1,1,1,0.2f), false);

		//	Create chunk entity
		chunkEntity = entityManager.CreateEntity(mapChunkArchetype);
		MapChunk chunkComponent = new MapChunk { worldPosition = position };
		entityManager.SetComponentData(chunkEntity, chunkComponent);

		//	Chunk is at map edge
		if(edge) entityManager.AddComponent(chunkEntity, typeof(MapEdge));

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