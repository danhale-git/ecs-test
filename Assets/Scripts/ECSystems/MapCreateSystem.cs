using UnityEngine;
using Unity.Jobs;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Transforms;
using Unity.Rendering;
using MyComponents;

[AlwaysUpdateSystem]
//	Create map squares based on player position
public class MapCreateSystem : ComponentSystem
{
	enum Buffer { NONE, INNER, OUTER, EDGE }

	EntityManager entityManager;

	public static Entity playerEntity;

	public static NativeArray<Entity> mapSquareMatrix;
	public static float3 previousMatrixRootPosition;	
	public static float3 matrixRootPosition;
	float3 center;

	//	Square data
	EntityArchetype mapSquareArchetype;

	//	Terrain settings
	static int cubeSize;
	static int viewDistance;
	static int viewDiameter;

	NativeList<Entity>		updateMapSquares;
	NativeList<Buffer>		updateBuffers;
							
	ComponentGroup allSquaresGroup;

	protected override void OnCreateManager()
	{
		cubeSize 		= TerrainSettings.cubeSize;
		viewDistance 	= TerrainSettings.viewDistance;
		viewDiameter	= (viewDistance*2)+3;

		entityManager = World.Active.GetOrCreateManager<EntityManager>();

        mapSquareArchetype = entityManager.CreateArchetype(
            ComponentType.Create<Position>(),
            ComponentType.Create<RenderMeshComponent>(),
            ComponentType.Create<MapSquare>(),
            ComponentType.Create<Topology>(),
            ComponentType.Create<Block>(),

            ComponentType.Create<Tags.GenerateTerrain>(),
            ComponentType.Create<Tags.GetAdjacentSquares>(),
            ComponentType.Create<Tags.SetDrawBuffer>(),
            ComponentType.Create<Tags.SetBlockBuffer>(),
            ComponentType.Create<Tags.GenerateBlocks>(),
            ComponentType.Create<Tags.SetSlopes>(),
			ComponentType.Create<Tags.DrawMesh>()
			);

		//	All map squares
		EntityArchetypeQuery allSquaresQuery = new EntityArchetypeQuery{		
			All 	= new ComponentType [] { typeof(MapSquare) }
		};

		allSquaresGroup = GetComponentGroup(allSquaresQuery);
	}

    protected override void OnDestroyManager()
	{
		if(mapSquareMatrix.IsCreated) mapSquareMatrix.Dispose();
	}

	float3 previousSquare = new float3(100, 100, 100);

	bool debug1 = true;

	//	Continually generate map squares
	protected override void OnUpdate()
	{
		if(mapSquareMatrix.IsCreated) mapSquareMatrix.Dispose();
		mapSquareMatrix = new NativeArray<Entity>((int)math.pow(viewDiameter, 2), Allocator.Persistent);

		float3 position = entityManager.GetComponentData<Position>(playerEntity).Value;
		float3 currentSquare = Util.VoxelOwner(position, cubeSize);
		float3 mapSquaresTraversed = (currentSquare - previousSquare) / cubeSize;
		
		//TODO: trim squares

		if(!currentSquare.Equals(previousSquare))
		{
			previousMatrixRootPosition = matrixRootPosition;

			int rootOffset = (viewDistance+1) * cubeSize; 
			
			matrixRootPosition = new float3(
				currentSquare.x - rootOffset,
				0,
				currentSquare.z - rootOffset
			);

			center = new Vector3(currentSquare.x, 0, currentSquare.z);

			previousSquare = currentSquare;
			GenerateRadius(currentSquare);
		}
	}

	

	void AddSquare(float3 position)
	{
		CustomDebugTools.Cube(Color.green, position + cubeSize / 2);
	}

	void CheckSquare(float3 position)
	{
		CustomDebugTools.Cube(Color.blue, position + cubeSize / 2);
	}

	void RemoveSquare(float3 position)
	{
		CustomDebugTools.Cube(Color.red, position + cubeSize / 2);
	}

	bool SquareInRadius(float3 position, float3 rootPosition)
	{
		if(	position.x >= rootPosition.x &&
			position.z >= rootPosition.z &&
			position.x <  rootPosition.x + ((viewDiameter) * cubeSize) &&
			position.z <  rootPosition.z + ((viewDiameter) * cubeSize))
		{
			return true;
		}
		else
			return false;
	}

	//	Create squares
	void GenerateRadius(Vector3 radiusCenter)
	{
		

		NativeList<Position> positions;
		NativeList<Buffer> buffers;

		PositionsAndBuffers(out positions, out buffers, center, viewDistance);

		CreateOrCheck(positions, buffers);

		for(int i = 0; i < positions.Length; i++)
			CreateSquare(positions[i].Value, buffers[i]);

		for(int i = 0; i < updateMapSquares.Length; i++)
			CheckBuffer(updateMapSquares[i], updateBuffers[i]);

		positions.Dispose();
		buffers.Dispose();

		updateMapSquares.Dispose();
		updateBuffers.Dispose();
	}

	void PositionsAndBuffers(out NativeList<Position> positions, out NativeList<Buffer> buffers, float3 center, int radius)
	{
		positions = new NativeList<Position>(Allocator.TempJob);
 		buffers = new NativeList<Buffer>(Allocator.TempJob);

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
				float3 position = new float3(x*cubeSize, 0, z*cubeSize) + center;

				positions.Add(new Position{ Value = position });
				buffers.Add(buffer);
			}
	}

	Buffer GetBuffer(float3 position, float3 center, int radius)
	{
		float3 local = (position - center)/cubeSize;

		Buffer buffer = 0;

		//	Chunk is at the edge of the map 		- edge buffer
		if 		(local.x < -radius || local.x >  radius 		|| local.z < -radius 		|| local.z >  radius )
			buffer = Buffer.EDGE;

		//	Chunk is next to the edge of the map 	- outer buffer
		else if	(local.x == -radius || local.x ==  radius 		|| local.z == -radius 	|| local.z ==  radius )
			buffer = Buffer.OUTER;

		//	Chunk is 1 from the edge of the map 	- inner buffer
		else if	(local.x == -radius+1 || local.x ==  radius-1 	|| local.z == -radius +1 	|| local.z ==  radius -1 )
			buffer = Buffer.INNER;

		//	Create map square at position
		return buffer;
	}

	//	Organise positions into existing and non existent map squares
	bool CreateOrCheck(NativeList<Position> radiusPositions, NativeList<Buffer> radiusBuffers)
	{
		ArchetypeChunkEntityType entityType	 	= GetArchetypeChunkEntityType();
		ArchetypeChunkComponentType<Position> positionType	= GetArchetypeChunkComponentType<Position>();

		updateMapSquares = new NativeList<Entity>(Allocator.Persistent);
		updateBuffers = new NativeList<Buffer>(Allocator.Persistent);
		
		//	All map squares
		NativeArray<ArchetypeChunk> chunks = allSquaresGroup.CreateArchetypeChunkArray(Allocator.Persistent);	

		for(int d = 0; d < chunks.Length; d++)
		{
			ArchetypeChunk chunk = chunks[d];

			NativeArray<Entity> entities 				= chunk.GetNativeArray(entityType);
			NativeArray<Position> mapSquarePositions 	= chunk.GetNativeArray(positionType);

			for(int e = 0; e < entities.Length; e++)
			{
				Entity entity = entities[e];

				for(int p = 0; p < radiusPositions.Length; p++)
				{
					Position radiusPosition = radiusPositions[p];
					Buffer buffer = radiusBuffers[p];

					if(Util.Float3sMatchXZ(radiusPosition.Value, mapSquarePositions[e].Value))
					{
						// collect entities
						updateMapSquares.Add(entity);
						updateBuffers.Add(buffer);

						radiusPositions.RemoveAtSwapBack(p);
						radiusBuffers.RemoveAtSwapBack(p);

						AddMapSquareToMatrix(entity, mapSquarePositions[e].Value);

						break;
					}
				}
			}
		}

		chunks.Dispose();
		return false;
	}

	void CreateSquare(Vector3 position, Buffer buffer)
	{
		//	Create square entity
		Entity entity = entityManager.CreateEntity(mapSquareArchetype);

		//	Set position
		entityManager.SetComponentData<Position>(entity, new Position{ Value = position } );

		switch(buffer)
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

		AddMapSquareToMatrix(entity, position);
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

	void AddMapSquareToMatrix(Entity entity, float3 position)
	{
		int2 index = MatrixIndex(position, viewDistance);
		mapSquareMatrix[Util.Flatten2D(index.x, index.y, viewDiameter)] = entity;
	}

	public static Entity GetMapSquareFromMatrix(float3 position)
	{
		int2 index = MatrixIndex(position, viewDistance);
		return mapSquareMatrix[Util.Flatten2D(index.x, index.y, viewDiameter)];
	}

	static int2 MatrixIndex(float3 worldPosition, int radius)
	{
		float3 local = worldPosition - matrixRootPosition;
		return new int2((int)local.x, (int)local.z) / cubeSize;
	}
}
