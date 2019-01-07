using UnityEngine;
using Unity.Jobs;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Transforms;
using Unity.Rendering;
using MyComponents;

//	Create map squares based on player position
public class MapSquareSystem : ComponentSystem
{
	enum Buffer { NONE, INNER, OUTER, EDGE }

	EntityManager entityManager;

	//	Player GameObject
	PlayerController player;

	//	Square data
	EntityArchetype mapSquareArchetype;

	//	Terrain settings
	int cubeSize;
	int viewDistance;

	NativeList<Position> 	createMapSquares;
	NativeList<Buffer>	 	createBuffers;
	NativeList<Entity>		updateMapSquares;
	NativeList<Buffer>		updateBuffers;
							
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

	Vector3 previousSquare;

	//	Continually generate map squares
	protected override void OnUpdate()
	{
		//	Generate map in radius around player
		Vector3 currentSquare = Util.VoxelOwner(player.transform.position, cubeSize);
		if(currentSquare != previousSquare)
		{
			previousSquare = currentSquare;
			GenerateRadius(currentSquare, viewDistance);
		}		
	}

	//	Create squares
	public void GenerateRadius(Vector3 center, int radius)
	{
		center = new Vector3(center.x, 0, center.z);

		int squaresCreated = 0;

		NativeList<Position> positions = new NativeList<Position>(Allocator.TempJob);
		NativeList<Buffer> buffers = new NativeList<Buffer>(Allocator.TempJob);

		//	Generate grid of map squares in radius
		for(int x = -radius-1; x <= radius+1; x++)
			for(int z = -radius-1; z <= radius+1; z++)
			{
				Buffer buffer = 0;
				//	Chunk is at the edge of the map 		- edge buffer
				if 		(x < -radius || x >  radius 		|| z < -radius 		|| z >  radius )
					buffer = Buffer.EDGE;
				//	Chunk is next to the edge of the map 	- outer buffer
				else if	(x == -radius || x ==  radius 		|| z == -radius 	|| z ==  radius )
					buffer = Buffer.OUTER;
				//	Chunk is 1 from the edge of the map 	- inner buffer
				else if	(x == -radius+1 || x ==  radius-1 	|| z == -radius +1 	|| z ==  radius -1 )
					buffer = Buffer.INNER;

				//	Create map square at position
				Vector3 offset = new Vector3(x*cubeSize, 0, z*cubeSize);

				positions.Add(new Position{ Value = center + offset });
				buffers.Add(buffer);
			}

		CreateUpdateRadius(positions, buffers);

		positions.Dispose();
		buffers.Dispose();

		for(int i = 0; i < createMapSquares.Length; i++)
		{
			CreateSquare(createMapSquares[i].Value, createBuffers[i]);
		}
		for(int i = 0; i < updateMapSquares.Length; i++)
		{
			CheckBuffer(updateMapSquares[i], updateBuffers[i]);
		}

		squaresCreated = createMapSquares.Length;

		createMapSquares.Dispose();
		createBuffers.Dispose();
		updateMapSquares.Dispose();
		updateBuffers.Dispose();
	}

	void CreateSquare(Vector3 position, Buffer buffer)
	{
		//	Create square entity
			Entity entity = entityManager.CreateEntity(mapSquareArchetype);

			//	Set position
			entityManager.SetComponentData(
				entity,
				new Position{ Value = position }
				);		

			//	Generate terrain next
			entityManager.AddComponent(entity, typeof(Tags.GenerateTerrain));

			SetBuffer(entity, buffer);
	}

	//	Set buffer type
	void SetBuffer(Entity entity, Buffer edge)
	{
		switch(edge)
		{
			//	Is inner buffer
			case Buffer.INNER:
				entityManager.AddComponent(entity, typeof(Tags.InnerBuffer));
				break;

			//	Is outer buffer
			case Buffer.OUTER:
				entityManager.AddComponent(entity, typeof(Tags.OuterBuffer));
				break;

			//	Is edge buffer
			case Buffer.EDGE:
				entityManager.AddComponent(entity, typeof(Tags.EdgeBuffer));
				break;
			
			//	Is not a buffer
			default:
				break;
		}
	}

	//	Check if buffer type needs updating
	void CheckBuffer(Entity entity, Buffer edge)
	{
		switch(edge)
		{
			//	Outer buffer changed to inner buffer
			case Buffer.INNER:
				if(entityManager.HasComponent<Tags.OuterBuffer>(entity))
				{
					entityManager.RemoveComponent<Tags.OuterBuffer>(entity);

					entityManager.AddComponent(entity, typeof(Tags.InnerBuffer));
				}	
				break;

			//	Edge buffer changed to outer buffer
			case Buffer.OUTER:
				if(entityManager.HasComponent<Tags.EdgeBuffer>(entity))
				{
					entityManager.RemoveComponent<Tags.EdgeBuffer>(entity);

					entityManager.AddComponent(entity, typeof(Tags.OuterBuffer));
				}
				break;

			//	Still edge buffer
			case Buffer.EDGE: break;
			
			//	Not a buffer
			default:
				if(entityManager.HasComponent<Tags.EdgeBuffer>(entity))
					entityManager.RemoveComponent<Tags.EdgeBuffer>(entity);

				if(entityManager.HasComponent<Tags.InnerBuffer>(entity))
					entityManager.RemoveComponent<Tags.InnerBuffer>(entity);
				break;
		}
	}

	//	Organise positions into existing and non existent map squares
	bool CreateUpdateRadius(NativeList<Position> radiusPositions, NativeList<Buffer> radiusBuffers)
	{
		entityType	 	= GetArchetypeChunkEntityType();
		positionType	= GetArchetypeChunkComponentType<Position>();

		createMapSquares = new NativeList<Position>(Allocator.Persistent);
		createBuffers = new NativeList<Buffer>(Allocator.Persistent);
		updateMapSquares = new NativeList<Entity>(Allocator.Persistent);
		updateBuffers = new NativeList<Buffer>(Allocator.Persistent);
		
		//	All map squares
		NativeArray<ArchetypeChunk> chunks = entityManager.CreateArchetypeChunkArray(
			mapSquareQuery,
			Allocator.TempJob
			);		

		for(int p = 0; p < radiusPositions.Length; p++)
		{
			Position radiusPosition = radiusPositions[p];
			Buffer buffer = radiusBuffers[p];

			bool foundEntity = false;

			for(int d = 0; d < chunks.Length; d++)
			{
				ArchetypeChunk chunk = chunks[d];

				NativeArray<Entity> entities 				= chunk.GetNativeArray(entityType);
				NativeArray<Position> mapSquarePositions 	= chunk.GetNativeArray(positionType);

				for(int e = 0; e < entities.Length; e++)
				{
					Entity entity = entities[e];

					if(	radiusPosition.Value.x == mapSquarePositions[e].Value.x &&
						radiusPosition.Value.z == mapSquarePositions[e].Value.z)
					{
						// collect entities
						updateMapSquares.Add(entity);
						updateBuffers.Add(buffer);

						foundEntity = true;
						continue;
					}
					if(foundEntity) continue;
				}
				if(foundEntity)continue;
			}

			if(!foundEntity)
			{
				createMapSquares.Add(radiusPosition);
				createBuffers.Add(buffer);
			}
		}

		chunks.Dispose();
		return false;
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
