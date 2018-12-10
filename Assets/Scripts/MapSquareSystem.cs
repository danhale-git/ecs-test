using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using MyComponents;

public class MapSquareSystem : ComponentSystem
{
	PlayerController player;

	EntityManager entityManager;

	//	Square data
	EntityArchetype mapSquareArchetype;

	int cubeSize;
	int viewDistance;
	int terrainHeight;

	ArchetypeChunkEntityType entityType;
	ArchetypeChunkComponentType<MapSquare> squareType;
	EntityArchetypeQuery mapSquareQuery;

	protected override void OnCreateManager()
	{
		cubeSize = TerrainSettings.cubeSize;
		viewDistance = TerrainSettings.viewDistance;
		terrainHeight = TerrainSettings.terrainHeight;

		player = GameObject.FindObjectOfType<PlayerController>();

		entityManager = World.Active.GetOrCreateManager<EntityManager>();

		mapSquareArchetype = entityManager.CreateArchetype(
				ComponentType.Create<MapSquare>(),
				ComponentType.Create<Height>(),
				ComponentType.Create<CubePosition>(),
				ComponentType.Create<Block>(),
				ComponentType.Create<CubeCount>()

			);

		//	All map squares
		mapSquareQuery = new EntityArchetypeQuery
		{
			Any = System.Array.Empty<ComponentType>(),
			None = System.Array.Empty<ComponentType>(),
			All = new ComponentType [] { typeof(MapSquare) }
		};
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
				Util.VoxelOwner(player.transform.position, cubeSize),
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

				Vector3 offset = new Vector3(x*cubeSize, 0, z*cubeSize);
				CreateSquare(center + offset, edge);
			}

	}

	void CreateSquare(Vector3 pos, bool edge)
	{
		Entity squareEntity;
		Vector2 position = new Vector2(pos.x, pos.z);

		bool exists = MapSquareAtPosition(position, out squareEntity);

		if(!exists)
		{
			Color debugColor = edge ? new Color(1f,.2f,.2f,0f) : new Color(.2f,1f,.2f,0.2f);
			CustomDebugTools.WireCubeChunk(pos, cubeSize, debugColor, true);

			//	Create square entity
			squareEntity = entityManager.CreateEntity(mapSquareArchetype);
			MapSquare squareComponent = new MapSquare { worldPosition = position };
			entityManager.SetComponentData(squareEntity, squareComponent);

			NativeArray<int> heightMap = GetHeightMap(
				new float3(position.x,0, position.y),
				cubeSize,
				5678,
				0.05f,
				terrainHeight);

			//	Get heightmap
			DynamicBuffer<Height> heightBuffer = entityManager.GetBuffer<Height>(squareEntity);
			heightBuffer.ResizeUninitialized((int)math.pow(cubeSize, 2));
			
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

			entityManager.AddComponent(squareEntity, typeof(Tags.GenerateBlocks));
			entityManager.AddComponent(squareEntity, typeof(Tags.CreateCubes));
		}

		if(!edge && !entityManager.HasComponent<Tags.DrawMesh>(squareEntity) &&
		   !entityManager.HasComponent<Tags.MeshDrawn>(squareEntity))
		{
			entityManager.AddComponent(squareEntity, typeof(Tags.DrawMesh));
			CustomDebugTools.WireCubeChunk(pos, cubeSize-2, new Color(0, 1, 0, 1f), true);
		}
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

	bool MapSquareAtPosition(float2 position, out Entity mapSquare)
	{
		entityType = GetArchetypeChunkEntityType();
		squareType = GetArchetypeChunkComponentType<MapSquare>();

		NativeArray<ArchetypeChunk> dataChunks = entityManager.CreateArchetypeChunkArray(mapSquareQuery, Allocator.TempJob);

		if(dataChunks.Length == 0)
		{
			dataChunks.Dispose();
			mapSquare = new Entity();
			return false;
		}

		for(int d = 0; d < dataChunks.Length; d++)
		{
			var dataChunk = dataChunks[d];
			var entities = dataChunk.GetNativeArray(entityType);
			var squares = dataChunk.GetNativeArray(squareType);

			for(int e = 0; e < entities.Length; e++)
			{
				var squareEntity = entities[e];
				var squareWorldPosition = squares[e].worldPosition;

				if(	position.x == squareWorldPosition.x &&
					position.y == squareWorldPosition.y)
				{
					mapSquare = squareEntity;
					dataChunks.Dispose();
					return true;
				}

			}
		}

		dataChunks.Dispose();
		mapSquare = new Entity();
		return false;
	}
}
