using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using Unity.Transforms;
using Unity.Rendering;
using MyComponents;

public class MapSquareSystem : ComponentSystem
{
	EntityManager entityManager;

	//	Player GameObject
	PlayerController player;

	//	Square data
	EntityArchetype mapSquareArchetype;

	//	Terrain settings
	int cubeSize;
	int viewDistance;
	int terrainHeight;

	ArchetypeChunkEntityType entityType;

	ArchetypeChunkComponentType<Position> 	positionType;


	EntityArchetypeQuery mapSquareQuery;

	protected override void OnCreateManager()
	{
		cubeSize 		= TerrainSettings.cubeSize;
		viewDistance 	= TerrainSettings.viewDistance;
		terrainHeight 	= TerrainSettings.terrainHeight;

		player = GameObject.FindObjectOfType<PlayerController>();

		entityManager = World.Active.GetOrCreateManager<EntityManager>();

		mapSquareArchetype = entityManager.CreateArchetype(
			ComponentType.Create<Position>(), 
			ComponentType.Create<MeshInstanceRendererComponent>(),
			ComponentType.Create<MapSquare>(),
			ComponentType.Create<Height>(),
			ComponentType.Create<MapCube>(),
			ComponentType.Create<Block>()	
			);

		//	All map squares
		mapSquareQuery = new EntityArchetypeQuery{		
			Any 	= System.Array.Empty<ComponentType>(),
			None 	= System.Array.Empty<ComponentType>(),
			All 	= new ComponentType [] { typeof(MapSquare) }
			};
	}


	//	Continually generate map squares
	float timer = -1f;
	bool debug = true;
	protected override void OnUpdate()
	{
		//	Timer
		 if(Time.fixedTime - timer > 1)
		{
			debug = false;
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

		//	Generate grid of map squares in radius
		for(int x = -radius; x <= radius; x++)
			for(int z = -radius; z <= radius; z++)
			{
				//	Chunk is at the edge of the map
				bool edge = (
					x == -radius ||
					x ==  radius ||
					z == -radius ||
					z ==  radius
					);

				Vector3 offset = new Vector3(x*cubeSize, 0, z*cubeSize);
				CreateSquare(center + offset, edge);
			}

	}

	//	Create map square and generate height map
	void CreateSquare(Vector3 position, bool edge)
	{
		if(position.y != 0)
			throw new System.ArgumentOutOfRangeException(
				"Map Square Y position must be 0: "+position.y
				);

		Entity squareEntity;	

		if(!GetMapSquares(position, out squareEntity))
		{
			Color debugColor = edge ? new Color(1,0,0,0.5f) : new Color(1,1,1,0.5f);
			CustomDebugTools.SetWireCubeChunk(position, cubeSize, debugColor);

			//	Create square entity
			squareEntity = entityManager.CreateEntity(mapSquareArchetype);

			//	Set position
			entityManager.SetComponentData(
			squareEntity,
			new Position{
				Value = position
			});

			//	Get heightmap
			NativeArray<int> heightMap = GetHeightMap(
				position,
				cubeSize,
				terrainHeight);

			//	Heightmap to Dynamic Buffer
			DynamicBuffer<Height> heightBuffer = entityManager.GetBuffer<Height>(squareEntity);
			heightBuffer.ResizeUninitialized((int)math.pow(cubeSize, 2));
			
			for(int h = 0; h < heightMap.Length; h++)
			{
				heightBuffer[h] = new Height
				{ 
					index 	= h,
					height 	= heightMap[h] + cubeSize
				};
			}

			heightMap.Dispose();

			DynamicBuffer<MapCube> cubeBuffer = entityManager.GetBuffer<MapCube>(squareEntity);
			MapCube cubePos1 = new MapCube { yPos = 0};
			MapCube cubePos2 = new MapCube { yPos = cubeSize};
			MapCube cubePos3 = new MapCube { yPos = cubeSize*2};
			MapCube cubePos4 = new MapCube { yPos = cubeSize*3};

			cubeBuffer.Add(cubePos1);
			cubeBuffer.Add(cubePos2);
			cubeBuffer.Add(cubePos3);
			cubeBuffer.Add(cubePos4);

			entityManager.AddComponent(squareEntity, typeof(Tags.GenerateBlocks));
			entityManager.AddComponent(squareEntity, typeof(Tags.DrawMesh));

			//	Leave a buffer of one to guarantee adjacent block data when culling faces	
			if(edge)
			{
				entityManager.AddComponent(squareEntity, typeof(Tags.MapEdge));
			}
		}
		else if (!edge && entityManager.HasComponent<Tags.MapEdge>(squareEntity))
		{
			//	Square is no longer at the edge of the map
			entityManager.RemoveComponent(squareEntity, typeof(Tags.MapEdge));
		}
	}

	public NativeArray<int> GetHeightMap(float3 chunkPosition, int chunkSize, int maxHeight)
    {
        int arrayLength = (int)math.pow(chunkSize, 2);

        var noiseMap = new NativeArray<float>(arrayLength, Allocator.TempJob);

        var job = new FastNoiseJob()
        {
            noiseMap 	= noiseMap,
			offset 		= chunkPosition,
			chunkSize	= chunkSize,
            seed 		= TerrainSettings.seed,
            frequency 	= TerrainSettings.frequency,
			util 		= new JobUtil(),
            noise 		= new SimplexNoiseGenerator(0)
        };

        job.Schedule(arrayLength, 16).Complete();

        job.noise.Dispose();

		NativeArray<int> heightMap = new NativeArray<int>(noiseMap.Length, Allocator.Temp);

		for(int i = 0; i < noiseMap.Length; i++)
		    heightMap[i] = (int)(noiseMap[i] * maxHeight);

		noiseMap.Dispose();

		return heightMap;
    }

	//	Get map square by position
	bool GetMapSquares(float3 position, out Entity mapSquare)
	{
		entityType = GetArchetypeChunkEntityType();

		positionType	= GetArchetypeChunkComponentType<Position>();

		NativeArray<ArchetypeChunk> chunks = entityManager.CreateArchetypeChunkArray(
			mapSquareQuery,
			Allocator.TempJob
			);

		if(chunks.Length == 0)
		{
			chunks.Dispose();
			mapSquare = new Entity();
			return false;
		}

		for(int d = 0; d < chunks.Length; d++)
		{
			ArchetypeChunk chunk = chunks[d];

			NativeArray<Entity> entities 	= chunk.GetNativeArray(entityType);
			NativeArray<Position> positions = chunk.GetNativeArray(positionType);

			for(int e = 0; e < entities.Length; e++)
			{
				Entity entity = entities[e];

				if(	position.x == positions[e].Value.x &&
					position.z == positions[e].Value.z)
				{
					mapSquare = entity;
					chunks.Dispose();
					return true;
				}

			}
		}

		chunks.Dispose();
		mapSquare = new Entity();
		return false;
	}

	//	Get multiple map squares by positions
	bool GetMapSquares(List<float3> positionsList, out Entity[] mapSquares)
	{
		List<Entity> mapSquaresList = new List<Entity>();

		entityType = GetArchetypeChunkEntityType();

		positionType = GetArchetypeChunkComponentType<Position>();

		NativeArray<ArchetypeChunk> chunks = entityManager.CreateArchetypeChunkArray(
			mapSquareQuery,
			Allocator.TempJob
			);

		if(chunks.Length == 0)
		{
			chunks.Dispose();
			mapSquares = mapSquaresList.ToArray();
			return false;
		}

		for(int d = 0; d < chunks.Length; d++)
		{
			ArchetypeChunk chunk = chunks[d];

			NativeArray<Entity> entities 	= chunk.GetNativeArray(entityType);
			NativeArray<Position> positions = chunk.GetNativeArray(positionType);

			for(int e = 0; e < entities.Length; e++)
			{
				Entity entity = entities[e];

				foreach(float3 position in positionsList)
				{

					if(	position.x == positions[e].Value.x &&
						position.z == positions[e].Value.z)
					{
						mapSquaresList.Add(entity);
						if(mapSquaresList.Count == positionsList.Count)
							break;
					}
				}
			}
		}

		chunks.Dispose();
		mapSquares = mapSquaresList.ToArray();

		if(mapSquares.Length == positionsList.Count)
			return true;
		else 
			return false;
	}
}
