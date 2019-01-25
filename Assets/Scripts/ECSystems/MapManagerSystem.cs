using UnityEngine;
using Unity.Jobs;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Transforms;
using Unity.Rendering;
using MyComponents;

[AlwaysUpdateSystem]
public class MapManagerSystem : ComponentSystem
{
	enum Buffer { NONE, INNER, OUTER, EDGE }
    
    EntityManager entityManager;

    static NativeArray<Entity> mapMatrix;
    static int cubeSize;
    static int matrixWidth;
    int matrixCenterOffset;

	public static Entity playerEntity;

    float3 currentMapSquare;
    float3 previousMapSquare;

    static float3 matrixRoot;
    float3 previousMatrixRoot;

    EntityArchetype mapSquareArchetype;

    ComponentGroup allSquaresGroup;

	protected override void OnCreateManager()
    {
		entityManager = World.Active.GetOrCreateManager<EntityManager>();

        cubeSize    = TerrainSettings.cubeSize;
        matrixWidth = (TerrainSettings.viewDistance * 2) + 1;
        matrixCenterOffset  = TerrainSettings.viewDistance;

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

    protected override void OnStartRunning()
    {
        //  Set previous square and matrix root position to !+ current
        UpdateCurrentMapSquare();
        this.previousMapSquare = currentMapSquare + (new float3(100, 100, 100) * cubeSize);
        
        this.previousMatrixRoot = new float3(
            previousMapSquare.x - (matrixCenterOffset * cubeSize),
            0,
            previousMapSquare.z - (matrixCenterOffset * cubeSize)
        );
    }

    protected override void OnUpdate()
    {
        //  Reset matrix array
        int matrixLength = (int)math.pow(matrixWidth, 2);
        if(mapMatrix.IsCreated) mapMatrix.Dispose();
        mapMatrix = new NativeArray<Entity>(matrixLength, Allocator.Persistent);

        //  Update current positions
        UpdateCurrentMapSquare();
        UpdateMatrixRoot();

        //  Player moved to a different square
        if(!currentMapSquare.Equals(previousMapSquare))
        {
            CheckAllSquares();
            CreateSquares();
        }

        this.previousMapSquare = currentMapSquare;
        this.previousMatrixRoot = matrixRoot;
    }

    void CreateSquares()
    {
        for(int i = 0; i < mapMatrix.Length; i++)
        {

            float3 index = Util.Unflatten2D(i, matrixWidth);
            Buffer buffer = GetBuffer(index);

            float3 worldPosition = WorldPosition(index);


            if(SquareInRadius(worldPosition, previousMatrixRoot))
                continue;

            Entity entity = entityManager.CreateEntity(mapSquareArchetype);
		    entityManager.SetComponentData<Position>(entity, new Position{ Value = worldPosition } );

            mapMatrix[i] = entity;

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
        }
    }

    bool CheckAllSquares()
	{
        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);

		ArchetypeChunkEntityType entityType	 	= GetArchetypeChunkEntityType();
		ArchetypeChunkComponentType<Position> positionType	= GetArchetypeChunkComponentType<Position>();
		
		//	All map squares
		NativeArray<ArchetypeChunk> chunks = allSquaresGroup.CreateArchetypeChunkArray(Allocator.Persistent);	

		for(int d = 0; d < chunks.Length; d++)
		{
			ArchetypeChunk chunk = chunks[d];

			NativeArray<Entity> entities 	= chunk.GetNativeArray(entityType);
			NativeArray<Position> positions = chunk.GetNativeArray(positionType);

			for(int e = 0; e < entities.Length; e++)
			{
				Entity entity = entities[e];
				float3 position = positions[e].Value;

                bool inPreviousRadius   = SquareInRadius(position, previousMatrixRoot);
                bool inCurrentRadius    = SquareInRadius(position, matrixRoot);

                if(inPreviousRadius && inCurrentRadius)
                {
                    UpdateBuffer(entity, GetBuffer(MatrixIndex(position)), commandBuffer);
                    AddMapSquareToMatrix(entity, position);
                }
			}
		}

        commandBuffer.Playback(entityManager);
		commandBuffer.Dispose();
		chunks.Dispose();
		return false;
	}

    //	Check if buffer type needs updating
	void UpdateBuffer(Entity entity, Buffer edge, EntityCommandBuffer commandBuffer)
	{
		switch(edge)
		{
			//	Outer buffer changed to inner buffer
			case Buffer.INNER:
				if(entityManager.HasComponent<Tags.OuterBuffer>(entity))
				{
					commandBuffer.RemoveComponent<Tags.OuterBuffer>(entity);

					commandBuffer.AddComponent<Tags.InnerBuffer>(entity, new Tags.InnerBuffer());
				}	
				break;

			//	Edge buffer changed to outer buffer
			case Buffer.OUTER:
				if(entityManager.HasComponent<Tags.EdgeBuffer>(entity))
				{
					commandBuffer.RemoveComponent<Tags.EdgeBuffer>(entity);

					commandBuffer.AddComponent<Tags.OuterBuffer>(entity, new Tags.OuterBuffer());
				}
				break;

			//	Still edge buffer
			case Buffer.EDGE: break;
			
			//	Not a buffer
			default:
				if(entityManager.HasComponent<Tags.EdgeBuffer>(entity))
					commandBuffer.RemoveComponent<Tags.EdgeBuffer>(entity);

				if(entityManager.HasComponent<Tags.InnerBuffer>(entity))
					commandBuffer.RemoveComponent<Tags.InnerBuffer>(entity);
				break;
		}
	}

    //TODO: collapse into caller or add components here
    Buffer GetBuffer(float3 index)
    {
        float3 worldPosition = WorldPosition(index) + (cubeSize/2);
        if(SquareInRing(index, 0))
        {
            //CustomDebugTools.Cube(Color.red, worldPosition);
            return Buffer.EDGE;
        }
        else if(SquareInRing(index, 1))
        {
            //CustomDebugTools.Cube(Color.green, worldPosition);
            return Buffer.OUTER;
        }
        else if(SquareInRing(index, 2))
        {
            //CustomDebugTools.Cube(Color.blue, worldPosition);
            return Buffer.INNER;
        }
        else
        {
            //CustomDebugTools.Cube(Color.white, worldPosition);
            return Buffer.NONE;
        }
    }

    //  Set currentMapSquare to position of map square player is in
    void UpdateCurrentMapSquare()
    {
        float3 playerPosition = entityManager.GetComponentData<Position>(playerEntity).Value;
        this.currentMapSquare = Util.VoxelOwner(playerPosition, cubeSize);
    }

    //  Set matrixRootMapSquare to position of bottom left square in matrix
    void UpdateMatrixRoot()
    {
        matrixRoot = new float3(
            currentMapSquare.x - (matrixCenterOffset * cubeSize),
            0,
            currentMapSquare.z - (matrixCenterOffset * cubeSize)
        );
    }

    bool SquareInRing(float3 index, int offset = 0)
	{
		if(	index.x == offset ||
			index.z == offset ||
			index.x ==  (matrixWidth - 1) - offset ||
			index.z ==  (matrixWidth - 1) - offset )
		{
			return true;
		}
		else
        {
			return false;
        }
	}

    bool SquareInRadius(float3 worldPosition, float3 matrixRootPosition)
	{
        float3 index = (worldPosition - matrixRootPosition) / cubeSize;

        int arrayWidth = matrixWidth-1;

		if(	index.x >= 0 && index.x <= arrayWidth &&
			index.z >= 0 && index.z <= arrayWidth )
		{
			return true;
		}
		else
        {
			return false;
        }
	}

    float3 MatrixIndex(float3 worldPosition)
    {
        return (worldPosition - matrixRoot) / cubeSize;
    }

    float3 WorldPosition(float3 index)
    {
        return (index * cubeSize) + matrixRoot;
    }

    public static Entity GetMapSquareFromMatrix(float3 worldPosition)
	{
		float3 index = (worldPosition - matrixRoot) / cubeSize;
		return mapMatrix[Util.Flatten2D(index.x, index.z, matrixWidth)];
	}

    void AddMapSquareToMatrix(Entity entity, float3 position)
	{
		float3 index = MatrixIndex(position);
		mapMatrix[Util.Flatten2D(index.x, index.z, matrixWidth)] = entity;
	}
}
