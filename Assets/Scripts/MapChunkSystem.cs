using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class MapChunkSystem : ComponentSystem
{
	PlayerController player;

	EntityManager entityManager;

	//	Chunk data
	EntityArchetype chunkArchetype;

	//	All chunks
	public static Dictionary<float3, Entity> map;

	public static int chunkSize = 8;
	public static int viewDistance = 8;

	protected override void OnCreateManager()
	{
		player = GameObject.FindObjectOfType<PlayerController>();

		entityManager = World.Active.GetOrCreateManager<EntityManager>();
		map = new Dictionary<float3, Entity>();

		chunkArchetype = entityManager.CreateArchetype(
				ComponentType.Create<MapChunk>(),
				ComponentType.Create<Block>(),
				ComponentType.Create<CREATE>()
			);

		//	Initial generation
		GenerateRadius(Vector3.zero, viewDistance);
	}


	//	Continuous generation
	float timer = 0;
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
				bool edge = false;
				if( x == -radius ||
					x ==  radius ||
					z == -radius ||
					z ==  radius    )
					edge = true;

				Vector3 offset = new Vector3(x*chunkSize, 0, z*chunkSize);
				CreateNewChunk(center + offset, edge);
			}

	}

	//	Create one chunk
	private void CreateNewChunk(Vector3 position, bool edge)
	{
		Entity chunkEntity;

		//	Chunk already exists
		if(map.TryGetValue(position, out chunkEntity))
		{
			//	Chunk was at the edge but isn't now
			if(!edge && entityManager.HasComponent(chunkEntity, typeof(MapEdge)) )
			{
				//	Remove MapEdge tag
				entityManager.RemoveComponent(chunkEntity, typeof(MapEdge));
			}
			return;
		}

		//	Create chunk entity
		chunkEntity = entityManager.CreateEntity(chunkArchetype);
		MapChunk chunkComponent = new MapChunk { worldPosition = position };
		entityManager.SetComponentData(chunkEntity, chunkComponent);

		//	Chunk is at map edge
		if(edge) entityManager.AddComponent(chunkEntity, typeof(MapEdge));

		map.Add(position, chunkEntity);
	}

	public static Entity[] GetAdjacentSquares(Vector3 position)
	{
		float3 pos = position;
		Entity[] adjacent = new Entity[6];
		
		adjacent[0] = map[pos +(new float3( 1,	0, 0) * chunkSize)];
		adjacent[1] = map[pos +(new float3(-1,	0, 0) * chunkSize)];

		//adjacent[2] = map[pos +(new float3( 0,	1, 0) * chunkSize)];
		//adjacent[3] = map[pos +(new float3( 0,-1, 0) * chunkSize)];
		adjacent[2] = map[pos +(new float3( 0,	0, 0) * chunkSize)];
		adjacent[3] = map[pos +(new float3( 0, 0, 0) * chunkSize)];

		adjacent[4] = map[pos +(new float3( 0,	0, 1) * chunkSize)];
		adjacent[5] = map[pos +(new float3( 0,	0,-1) * chunkSize)];

		return adjacent;
	}
}