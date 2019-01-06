using UnityEngine;
using Unity.Jobs;
using Unity.Entities;
using Unity.Mathematics;
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


	ArchetypeChunkEntityType 				entityType;
	ArchetypeChunkComponentType<Position> 	positionType;

	EntityArchetypeQuery mapSquareQuery;

	protected override void OnCreateManager()
	{
		cubeSize 		= TerrainSettings.cubeSize;
		viewDistance 	= TerrainSettings.viewDistance;
		

		player = GameObject.FindObjectOfType<PlayerController>();

		entityManager = World.Active.GetOrCreateManager<EntityManager>();

        mapSquareArchetype = entityManager.CreateArchetype(
            ComponentType.Create<Position>(),
            ComponentType.Create<MeshInstanceRendererComponent>(),
            ComponentType.Create<MapSquare>(),
            ComponentType.Create<Topology>(),
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

		/*//	Generate grid of map squares in radius
		for(int x = -radius; x <= radius; x++)
			for(int z = -radius; z <= radius; z++)
			{
				int buffer = 0;
				//	Chunk is at the edge of the map 	- outer buffer
				if (x == -radius || x ==  radius ||
					z == -radius || z ==  radius )
					buffer = 2;
				//	Chunk is 1 from the edge of the map - inner buffer
				else if(x == -radius +1 || x ==  radius -1 ||
						z == -radius +1 || z ==  radius -1 )
					buffer = 1;

				//	Create map square at position
				Vector3 offset = new Vector3(x*cubeSize, 0, z*cubeSize);
				squaresCreated += CreateSquare(center + offset, buffer);
			} */


		//	Generate grid of map squares in radius
		for(int x = -radius-1; x <= radius+1; x++)
			for(int z = -radius-1; z <= radius+1; z++)
			{
				int buffer = 0;
				//	Chunk is at the edge of the map 	- edge buffer
				if (x < -radius || x >  radius ||
					z < -radius || z >  radius )
					buffer = 3;
				//	Chunk is next to the edge of the map 	- outer buffer
				else if (x == -radius || x ==  radius ||
						 z == -radius || z ==  radius )
					buffer = 2;
				//	Chunk is 1 from the edge of the map - inner buffer
				else if(x == -radius +1 || x ==  radius -1 ||
						z == -radius +1 || z ==  radius -1 )
					buffer = 1;

				//	Create map square at position
				Vector3 offset = new Vector3(x*cubeSize, 0, z*cubeSize);
				squaresCreated += CreateSquare(center + offset, buffer);
			} 
	}

	//	Create map squares.
	int CreateSquare(Vector3 position, int buffer)
	{
		//	Map squares have a 2D position in a 3D space
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

			//	Generate terrain next
			entityManager.AddComponent(entity, typeof(Tags.GenerateTerrain));

			SetBuffer(entity, buffer, position);

			return 1;
		}
		
		CheckBuffer(entity, buffer, position);
		//CustomDebugTools.MapBufferDebug(entity);
		return 0;
	}

	//	Set buffer type
	void SetBuffer(Entity entity, int edge, float3 position)
	{
		switch(edge)
		{
			//	Is inner buffer
			case 1:
				entityManager.AddComponent(entity, typeof(Tags.InnerBuffer));
				break;

			//	Is outer buffer
			case 2:
				entityManager.AddComponent(entity, typeof(Tags.OuterBuffer));
				break;

			//	Is edge buffer
			case 3:
				entityManager.AddComponent(entity, typeof(Tags.EdgeBuffer));
				break;
			
			//	Is not a buffer
			default:
				break;
		}
	}

	//	Check if buffer type needs updating
	void CheckBuffer(Entity entity, int edge, float3 position)
	{
		switch(edge)
		{
			//	Outer buffer changed to inner buffer
			case 1:
				if(entityManager.HasComponent<Tags.OuterBuffer>(entity))
				{
					entityManager.RemoveComponent<Tags.OuterBuffer>(entity);

					entityManager.AddComponent(entity, typeof(Tags.InnerBuffer));
				}	
				break;

			//	Edge buffer changed to outer buffer
			case 2:
				if(entityManager.HasComponent<Tags.EdgeBuffer>(entity))
				{
					entityManager.RemoveComponent<Tags.EdgeBuffer>(entity);

					entityManager.AddComponent(entity, typeof(Tags.OuterBuffer));
				}
				break;

			//	Still edge buffer
			case 3: break;
			
			//	Not a buffer
			default:
				if(entityManager.HasComponent<Tags.EdgeBuffer>(entity))
					entityManager.RemoveComponent<Tags.EdgeBuffer>(entity);

				if(entityManager.HasComponent<Tags.InnerBuffer>(entity))
					entityManager.RemoveComponent<Tags.InnerBuffer>(entity);
				break;
		}
	}

	

	//	Get map square by position using chunk iteration
	bool GetMapSquare(float3 position, out Entity mapSquare)
	{
		entityType	 	= GetArchetypeChunkEntityType();
		positionType	= GetArchetypeChunkComponentType<Position>();

		//	All map squares
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

				//	If position matches return
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
