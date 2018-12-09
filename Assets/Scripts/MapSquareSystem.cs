using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Unity.Jobs;
using Unity.Collections;

public class MapSquareSystem : ComponentSystem
{
	PlayerController player;

	EntityManager entityManager;

	//	Chunk data
	EntityArchetype mapSquareArchetype;

	//	All chunks
	public static Dictionary<float2, Entity> map;

	int chunkSize;
	int viewDistance;
	int terrainHeight;

	protected override void OnCreateManager()
	{
		chunkSize = TerrainSettings.chunkSize;
		viewDistance = TerrainSettings.viewDistance;
		terrainHeight = TerrainSettings.terrainHeight;

		player = GameObject.FindObjectOfType<PlayerController>();

		entityManager = World.Active.GetOrCreateManager<EntityManager>();
		map = new Dictionary<float2, Entity>();

		mapSquareArchetype = entityManager.CreateArchetype(
				ComponentType.Create<MapSquare>(),
				ComponentType.Create<Height>()
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

	//	Create squares
	public void GenerateRadius(Vector3 center, int radius)
	{
		center = new Vector3(center.x, 0, center.z);

		//	Generate map in square
		for(int x = -radius; x <= radius; x++)
			for(int z = -radius; z <= radius; z++)
			{
				//	Chunk is at the edge of the map
				bool edge=( x == -radius ||
							x ==  radius ||
							z == -radius ||
							z ==  radius   );

				Vector3 offset = new Vector3(x*chunkSize, 0, z*chunkSize);
				CreateSquare(center + offset, edge);
			}

	}

	void CreateSquare(Vector3 pos, bool edge)
	{
		Entity squareEntity;
		Vector2 position = new Vector2(pos.x, pos.z);

		//	Square already exists
		if(map.TryGetValue(position, out squareEntity))
		{
			//	Square was at the edge but isn't now
			if(!edge && entityManager.HasComponent(squareEntity, typeof(MapEdge)) )
			{
				//	Remove MapEdge tag
				entityManager.RemoveComponent(squareEntity, typeof(MapEdge));
			}
			return;
		}

		Color debugColor = edge ? new Color(1f,.2f,.2f,0.2f) : new Color(.2f,1f,.2f,0.2f);
		CustomDebugTools.WireCubeChunk(pos, chunkSize, debugColor, true);

		//	Create square entity
		squareEntity = entityManager.CreateEntity(mapSquareArchetype);
		MapSquare squareComponent = new MapSquare { worldPosition = position };
		entityManager.SetComponentData(squareEntity, squareComponent);

		NativeArray<int> heightMap = GetHeightMap(
			new float3(position.x,0, position.y),
			chunkSize,
			5678,
			0.05f,
			terrainHeight);

		//	Get heightmap
		DynamicBuffer<Height> heightBuffer = entityManager.GetBuffer<Height>(squareEntity);
		heightBuffer.ResizeUninitialized((int)math.pow(chunkSize, 2));
		
		//	Fill Dynamic Buffer
		for(int h = 0; h < heightMap.Length; h++)
		{
			heightBuffer[h] = new Height
			{ 
				index = h,
				height = heightMap[h],
				localPosition = position
			};
		}

		heightMap.Dispose();

		map.Add(position, squareEntity);
	}

	public NativeArray<int> GetHeightMap(float3 chunkPosition, int chunkSize, int seed, float frequency, int maxHeight)
    {
        int arrayLength = (int)math.pow(chunkSize, 2);

        var noiseMap = new NativeArray<float>(arrayLength, Allocator.TempJob);

        var job = new FastNoiseJob()
        {
            noiseMap = noiseMap,
			offset = chunkPosition,
			chunkSize = chunkSize,
            seed = seed,
            frequency = frequency,
			util = new JobUtil(),
            noise = new SimplexNoiseGenerator(0)
        };

        JobHandle jobHandle = job.Schedule(arrayLength, 16);
        jobHandle.Complete();

        job.noise.Dispose();

		NativeArray<int> heightMap = new NativeArray<int>(noiseMap.Length, Allocator.Temp);

		for(int i = 0; i < noiseMap.Length; i++)
		    heightMap[i] = (int)(noiseMap[i] * maxHeight);

		noiseMap.Dispose();

		return heightMap;
    }

}
