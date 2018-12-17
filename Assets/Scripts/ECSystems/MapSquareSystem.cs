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
	int terrainStretch;

	ArchetypeChunkEntityType 				entityType;
	ArchetypeChunkComponentType<Position> 	positionType;

	EntityArchetypeQuery mapSquareQuery;

	protected override void OnCreateManager()
	{
		cubeSize 		= TerrainSettings.cubeSize;
		viewDistance 	= TerrainSettings.viewDistance;
		terrainHeight 	= TerrainSettings.terrainHeight;
		terrainStretch 	= TerrainSettings.terrainStretch;

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
		//	Generate map in radius around player
			GenerateRadius(
				Util.VoxelOwner(player.transform.position, cubeSize),
				viewDistance);			
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
				int edge = 0;
				//	Chunk is at the edge of the map
				if (x == -radius || x ==  radius ||
					z == -radius || z ==  radius )
					edge = 2;
				//	Chunk is 1 away from the edge of the map
				else if(x == -radius +1 || x ==  radius -1 ||
						z == -radius +1 || z ==  radius -1 )
					edge = 1;

				//	Create map square at position
				Vector3 offset = new Vector3(x*cubeSize, 0, z*cubeSize);
				squaresCreated += CreateSquare(center + offset, edge);
			}
	}

	//	Create map square and generate height map
	int CreateSquare(Vector3 position, int edge)
	{
		if(position.y != 0)
			throw new System.ArgumentException(
				"Map Square Y position must be 0: "+position.y
				);

		Entity entity;	

		//	Square does not exist yet
		if(!GetMapSquare(position, out entity))
		{
			//	Create square entity
			entity = entityManager.CreateEntity(mapSquareArchetype);

			//	Set position
			entityManager.SetComponentData(
				entity,
				new Position{ Value = position }
				);

			//	Resize to Dynamic Buffer
			DynamicBuffer<Height> heightBuffer = entityManager.GetBuffer<Height>(entity);
			heightBuffer.ResizeUninitialized((int)math.pow(cubeSize, 2));

			//	Fill buffer with height map data
			MapSquare mapSquareComponent = GetHeightMap(position, heightBuffer);
			entityManager.SetComponentData<MapSquare>(entity, mapSquareComponent);

			//	Create cubes next
			entityManager.AddComponent(entity, typeof(Tags.CreateCubes));

			SetBuffer(entity, edge, position);

			return 1;
		}
		
		CheckBuffer(entity, edge, position);
		
		return 0;
	}

	void SetBuffer(Entity entity, int edge, float3 position)
	{
		switch(edge)
		{
			//	Is inner buffer
			case 1:
				entityManager.AddComponent(entity, typeof(Tags.InnerBuffer));
				CustomDebugTools.SetMapSquareHighlight(entity, cubeSize, new Color(1,0.5f,0.5f,0.2f));	
				break;

			//	Is outer buffer
			case 2:
				entityManager.AddComponent(entity, typeof(Tags.OuterBuffer));
				CustomDebugTools.SetMapSquareHighlight(entity, cubeSize, new Color(0.5f,1,0.5f,0.2f));
				break;
			
			//	Is not a buffer
			default:
				CustomDebugTools.SetMapSquareHighlight(entity, cubeSize, new Color(0.5f,1,1,0.2f));				
				break;
		}
	}

	void CheckBuffer(Entity entity, int edge, float3 position)
	{
		switch(edge)
		{
			//	Outer buffer changed to innter buffer
			case 1:
				if(entityManager.HasComponent<Tags.OuterBuffer>(entity))
				{
					entityManager.RemoveComponent<Tags.OuterBuffer>(entity);

					entityManager.AddComponent(entity, typeof(Tags.InnerBuffer));
					CustomDebugTools.SetMapSquareHighlight(entity, cubeSize, new Color(1,0.5f,0.5f,0.2f));
				}	
				break;

			//	Still outer buffer, do nothing
			case 2: break;
			
			//	Not a buffer
			default:
				if(entityManager.HasComponent<Tags.OuterBuffer>(entity))
					entityManager.RemoveComponent<Tags.OuterBuffer>(entity);

				if(entityManager.HasComponent<Tags.InnerBuffer>(entity))
					entityManager.RemoveComponent<Tags.InnerBuffer>(entity);

				CustomDebugTools.SetMapSquareHighlight(entity, cubeSize, new Color(0.5f,1,1,0.2f));				
				break;
		}
	}

	public MapSquare GetHeightMap(float3 position, DynamicBuffer<Height> heightMap)
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

		//	Convert noise (0-1) into heights (0-maxHeight)
		//	TODO: Jobify this
		int highestBlock = 0;
		int lowestBlock = terrainHeight;
		for(int i = 0; i < noiseMap.Length; i++)
		{
			int height = (int)((noiseMap[i] * terrainStretch) + terrainHeight);
		    heightMap[i] = new Height { height = height };
				
			if(height > highestBlock)
				highestBlock = height;
			if(height < lowestBlock)
				lowestBlock = height;
		}

		//	Dispose of NativeArrays in noise struct
        job.noise.Dispose();
		noiseMap.Dispose();

		return new MapSquare{
			highestBlock 	= highestBlock,
			lowestBlock 	= lowestBlock
			};
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
