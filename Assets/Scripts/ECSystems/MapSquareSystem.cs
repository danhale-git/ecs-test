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

	ArchetypeChunkEntityType 				entityType;
	ArchetypeChunkComponentType<Position> 	positionType;

	EntityArchetypeQuery mapSquareQuery;

	float timer = -1f;
	float3 previousMapSquare;

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
	
	bool debug = true;
	protected override void OnUpdate()
	{
		float3 currentMapSquare = SquarePosition(player.transform.position);
		
		//	Generate map in radius around player
			GenerateRadius(
				currentMapSquare,
				viewDistance);
				
		//	Timer
		 if(Time.fixedTime - timer > 1)
		{
			debug = false;
			timer = Time.fixedTime;
			
		}
	}

	float3 SquarePosition(float3 pointInWorld)
	{
		int x = ((int)math.floor(pointInWorld.x / cubeSize)) * cubeSize;
		int y = ((int)math.floor(pointInWorld.y / cubeSize)) * cubeSize;
		int z = ((int)math.floor(pointInWorld.z / cubeSize)) * cubeSize;
		return new float3(x, y, z);
	}

	//	Create squares
	public void GenerateRadius(Vector3 center, int radius)
	{
		center = new Vector3(center.x, 0, center.z);

		int squaresCreated = 0;

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
				squaresCreated += CreateSquare(center + offset, edge);
			}
	}

	//	Create map square and generate height map
	int CreateSquare(Vector3 position, bool edge)
	{
		if(position.y != 0)
			throw new System.ArgumentException(
				"Map Square Y position must be 0: "+position.y
				);

		Entity squareEntity;	

		//	Square does not exist yet
		if(!GetMapSquare(position, out squareEntity))
		{
			Color debugColor = edge ? new Color(0,1,0,0.2f) : new Color(1,1,1,0.2f);
			CustomDebugTools.SetWireCubeChunk(position, cubeSize, debugColor);

			//	Create square entity
			squareEntity = entityManager.CreateEntity(mapSquareArchetype);

			//	Set position
			entityManager.SetComponentData(
			squareEntity,
			new Position{ Value = position });

			//	Get heightmap
			NativeArray<int> heightMap = GetHeightMap(position);

			//	Heightmap to Dynamic Buffer
			DynamicBuffer<Height> heightBuffer = entityManager.GetBuffer<Height>(squareEntity);
			heightBuffer.ResizeUninitialized((int)math.pow(cubeSize, 2));

			int highestBlock = 0;
			int lowestBlock = terrainHeight;
			
			for(int h = 0; h < heightMap.Length; h++)
			{
				int height = heightMap[h] + cubeSize;
				heightBuffer[h] = new Height
				{ 
					height = height
				};

				if(height > highestBlock)
					highestBlock = height;
				if(height < lowestBlock)
					lowestBlock = height;
			}

			heightMap.Dispose();

			MapSquare mapSquareComponent = new MapSquare{
				highestBlock 	= highestBlock,
				lowestBlock 	= lowestBlock
			};
			entityManager.SetComponentData<MapSquare>(squareEntity, mapSquareComponent);

			//	Create cubes
			DynamicBuffer<MapCube> cubeBuffer = entityManager.GetBuffer<MapCube>(squareEntity);

			//	TODO: Proper cube terrain height checks and cube culling
			MapCube cubePos1 = new MapCube { yPos = 0};
			MapCube cubePos2 = new MapCube { yPos = cubeSize};
			MapCube cubePos3 = new MapCube { yPos = cubeSize*2};
			MapCube cubePos4 = new MapCube { yPos = cubeSize*3};

			cubeBuffer.Add(cubePos1);
			cubeBuffer.Add(cubePos2);
			cubeBuffer.Add(cubePos3);
			cubeBuffer.Add(cubePos4);

			//	Add tags to generate block data and draw mesh
			entityManager.AddComponent(squareEntity, typeof(Tags.GenerateBlocks));
			entityManager.AddComponent(squareEntity, typeof(Tags.DrawMesh));

			//	Leave a buffer of one to guarantee adjacent block data when culling faces	
			if(edge)
				entityManager.AddComponent(squareEntity, typeof(Tags.MapEdge));

			return 1;
		}
		//	Square exists and used to be at the edge of the map
		else if (!edge && entityManager.HasComponent<Tags.MapEdge>(squareEntity))
			entityManager.RemoveComponent(squareEntity, typeof(Tags.MapEdge)); return 0;
	}

	public NativeArray<int> GetHeightMap(float3 position)
    {
		//	Flattened 2D array noise data matrix
        var noiseMap = new NativeArray<float>((int)math.pow(cubeSize, 2), Allocator.TempJob);

        var job = new FastNoiseJob(){
            noiseMap 	= noiseMap,						//	Noise map matrix to be filled
			offset 		= position,						//	Position of this map square
			cubeSize	= cubeSize,						//	Length of one side of a square/cube	
            seed 		= TerrainSettings.seed,			//	Perlin noise seed
            frequency 	= TerrainSettings.frequency,	//	Perlin noise frequency
			util 		= new JobUtil(),				//	Utilities
            noise 		= new SimplexNoiseGenerator(0)	//	FastNoise.GetSimplex adapted for Jobs
        };

        job.Schedule(noiseMap.Length, 16).Complete();

		//	Dispose of NativeArrays in noise struct
        job.noise.Dispose();

		//	Convert noise (0-1) into heights (0-maxHeight)
		NativeArray<int> heightMap = new NativeArray<int>(noiseMap.Length, Allocator.Temp);

		//	TODO: Jobify this
		for(int i = 0; i < noiseMap.Length; i++)
		    heightMap[i] = (int)(noiseMap[i] * terrainHeight);

		noiseMap.Dispose();

		return heightMap;
    }

	//	Get map square by position
	bool GetMapSquare(float3 position, out Entity mapSquare)
	{
		entityType	 	= GetArchetypeChunkEntityType();
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
}
