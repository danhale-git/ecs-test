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
    static float3 matrixRootMapSquare;

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
        //  Set previous square to != current square
        UpdatePlayerMapSquare();
        this.previousMapSquare = currentMapSquare + (new float3(1, 1, 1) * cubeSize);
        Debug.Log("player: "+currentMapSquare);
    }

    protected override void OnUpdate()
    {
        //  Reset matrix array
        int matrixLength = (int)math.pow(matrixWidth, 2);
        if(mapMatrix.IsCreated) mapMatrix.Dispose();
        mapMatrix = new NativeArray<Entity>(matrixLength, Allocator.Persistent);

        //  Update map square
        UpdatePlayerMapSquare();
        UpdateMatrixRoot();

        //  Player moved to a different square
        if(!currentMapSquare.Equals(previousMapSquare))
        {
            CreateSquares();
        }

        this.previousMapSquare = currentMapSquare;
    }

    void CreateSquares()
    {
        for(int i = 0; i < mapMatrix.Length; i++)
        {
            float3 index = Util.Unflatten2D(i, matrixWidth);
            Buffer buffer = GetBuffer(index);

            float3 worldPosition = MatrixWorldPosition(index);

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

    float3 MatrixIndex(float3 worldPosition)
    {
        return (worldPosition - matrixRootMapSquare) / cubeSize;
    }
    float3 MatrixWorldPosition(float3 index)
    {
        return (index * cubeSize) + matrixRootMapSquare;
    }

    Buffer GetBuffer(float3 index)
    {
        float3 worldPosition = MatrixWorldPosition(index) + (cubeSize/2);
        if(SquareInRing(index, 0))
        {
            CustomDebugTools.Cube(Color.red, worldPosition);
            return Buffer.EDGE;
        }
        else if(SquareInRing(index, 1))
        {
            CustomDebugTools.Cube(Color.green, worldPosition);
            return Buffer.OUTER;
        }
        else if(SquareInRing(index, 2))
        {
            CustomDebugTools.Cube(Color.blue, worldPosition);
            return Buffer.INNER;
        }
        else
        {
            CustomDebugTools.Cube(Color.white, worldPosition);
            return Buffer.NONE;
        }
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

    /*bool CheckAllSquares(float3 previousMatrixRoot)
	{
		ArchetypeChunkEntityType entityType	 	= GetArchetypeChunkEntityType();
		ArchetypeChunkComponentType<Position> positionType	= GetArchetypeChunkComponentType<Position>();
		
		//	All map squares
		NativeArray<ArchetypeChunk> chunks = allSquaresGroup.CreateArchetypeChunkArray(Allocator.Persistent);	

		for(int d = 0; d < chunks.Length; d++)
		{
			ArchetypeChunk chunk = chunks[d];

			NativeArray<Entity> entities 				= chunk.GetNativeArray(entityType);
			NativeArray<Position> positions 	= chunk.GetNativeArray(positionType);

			for(int e = 0; e < entities.Length; e++)
			{
				Entity entity = entities[e];
				float3 position = positions[e].Value;

				bool thisSquare = SquareInRadius(position, mapSquareMatrixRootPosition);
				bool previousSquare = SquareInRadius(position, previousMatrixRoot);

				if(thisSquare && previousSquare)
				{	
					Buffer buffer = GetBufferFromMatrix(position);
					CheckSquare(position, entity, buffer);
				}
				else if(!thisSquare && !previousSquare)
					RemoveSquare(position);

			}
		}

		chunks.Dispose();
		return false;
	} */

    //  Set currentMapSquare to position of map square player is in
    void UpdatePlayerMapSquare()
    {
        float3 playerPosition = entityManager.GetComponentData<Position>(playerEntity).Value;
        this.currentMapSquare = Util.VoxelOwner(playerPosition, cubeSize);
    }

    //  Set matrixRootMapSquare to position of bottom left square in matrix
    void UpdateMatrixRoot()
    {
        matrixRootMapSquare = new float3(
            currentMapSquare.x - (matrixCenterOffset * cubeSize),
            0,
            currentMapSquare.z - (matrixCenterOffset * cubeSize)
        );
    }

    public static Entity GetMapSquareFromMatrix(float3 worldPosition)
	{
		float3 index = (worldPosition - matrixRootMapSquare) / cubeSize;
		return mapMatrix[Util.Flatten2D(index.x, index.z, matrixWidth)];
	}
}
