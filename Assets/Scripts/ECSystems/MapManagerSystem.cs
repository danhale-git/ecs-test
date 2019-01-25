using UnityEngine;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Entities;
using Unity.Collections;

[AlwaysUpdateSystem]
public class MapManagerSystem : ComponentSystem
{
	enum Buffer { NONE, INNER, OUTER, EDGE }
    
    EntityManager entityManager;

    NativeArray<Entity> mapMatrix;
    int cubeSize;
    int matrixWidth;
    int rootOffset;

	public static Entity playerEntity;
    float3 currentMapSquare;
    float3 previousMapSquare;
    float3 matrixRootMapSquare;

	protected override void OnCreateManager()
    {
		entityManager = World.Active.GetOrCreateManager<EntityManager>();

        cubeSize    = TerrainSettings.cubeSize;
        matrixWidth = (TerrainSettings.viewDistance * 2) + 1;
        rootOffset  = TerrainSettings.viewDistance;

    }

    protected override void OnStartRunning()
    {
        //  Set previous square to != current square
        UpdatePlayerMapSquare();
        this.previousMapSquare = currentMapSquare + (new float3(1, 1, 1) * cubeSize);
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
            GetBuffer(index);
        }
    }

    Buffer GetBuffer(float3 index)
    {
        float3 worldPosition = ((index - rootOffset) * cubeSize) + matrixRootMapSquare;
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
            currentMapSquare.x - rootOffset,
            0,
            currentMapSquare.z - rootOffset
        );
    }
}
