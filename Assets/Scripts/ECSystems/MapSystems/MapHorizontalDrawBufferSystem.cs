using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Transforms;
using Unity.Rendering;
using UnityEngine;
using MyComponents;

[UpdateAfter(typeof(MapSquareCheckSystem))]
public class MapHorizontalDrawBufferSystem : ComponentSystem
{
	public enum DrawBufferType { NONE, INNER, OUTER, EDGE }

    EntityManager entityManager;
    EntityUtil entityUtil;

    MapCellMarchingSystem managerSystem;

    int squareWidth;

    ComponentGroup allSquaresGroup;
    ComponentGroup newSquaresGroup;

    struct SubMatrix
    {
        public readonly float3 rootPosition;
        public readonly int width;
        public SubMatrix(float3 rootPosition, int width)
        {
            this.rootPosition = rootPosition;
            this.width = width;
        }
    }

	protected override void OnCreateManager()
    {
        entityManager = World.Active.GetOrCreateManager<EntityManager>();
        managerSystem = World.Active.GetOrCreateManager<MapCellMarchingSystem>();

        squareWidth = TerrainSettings.mapSquareWidth;

        entityUtil = new EntityUtil(entityManager);


        EntityArchetypeQuery allSquaresQuery = new EntityArchetypeQuery{
            Any = new ComponentType [] { typeof(Tags.EdgeBuffer), typeof(Tags.OuterBuffer), typeof(Tags.InnerBuffer) },			
            All = new ComponentType [] { typeof(MapSquare) }
		};
		allSquaresGroup = GetComponentGroup(allSquaresQuery);

         EntityArchetypeQuery newSquaresQuery = new EntityArchetypeQuery{
            None = new ComponentType [] { typeof(Tags.EdgeBuffer), typeof(Tags.OuterBuffer), typeof(Tags.InnerBuffer) },
			All = new ComponentType [] { typeof(MapSquare) }
		};
		newSquaresGroup = GetComponentGroup(newSquaresQuery);
    }
    
    DrawBufferType GetDrawBuffer(SubMatrix square, float3 bufferWorldPosition)
    {

        if (IsOutsideSubMatrix(square, bufferWorldPosition)) return DrawBufferType.EDGE;
        else if (IsDistanceFromSubMatrixEdge(square, bufferWorldPosition, 0)) return DrawBufferType.EDGE;
        else if (IsDistanceFromSubMatrixEdge(square, bufferWorldPosition, 1)) return DrawBufferType.OUTER;
        else if (IsDistanceFromSubMatrixEdge(square, bufferWorldPosition, 2)) return DrawBufferType.INNER;
        else return DrawBufferType.NONE;
    }

    bool IsDistanceFromSubMatrixEdge(SubMatrix square, float3 bufferWorldPosition, int distance = 0)
	{
        float3 localPosition = (bufferWorldPosition - square.rootPosition) / squareWidth;

        if( (localPosition.x == 0+distance || localPosition.x == (square.width-1)-distance) ||
            (localPosition.z == 0+distance || localPosition.z == (square.width-1)-distance) )
            return true;
        else
            return false;
	}

    bool IsOutsideSubMatrix(SubMatrix square, float3 bufferWorldPosition)
	{
        float3 localPosition = (bufferWorldPosition - square.rootPosition) / squareWidth;

        if( localPosition.x < 0 || localPosition.x >= square.width ||
            localPosition.z < 0 || localPosition.z >= square.width )
            return true;
        else
            return false;
	}

    protected override void OnUpdate()
    {
        SubMatrix subMatrix = LargestSquare(managerSystem.mapMatrix.GetMatrix(), managerSystem.mapMatrix.rootPosition);

        subMatrix = TrimSubMatrix(subMatrix);
        
        SetNewSquares(subMatrix);

        CheckAllSquares(subMatrix);
        
    }

    void SetNewSquares(SubMatrix subMatrix)
    {
        EntityCommandBuffer         commandBuffer   = new EntityCommandBuffer(Allocator.Temp);
		NativeArray<ArchetypeChunk> chunks          = newSquaresGroup.CreateArchetypeChunkArray(Allocator.Persistent);

		ArchetypeChunkEntityType                entityType	    = GetArchetypeChunkEntityType();
		ArchetypeChunkComponentType<Position>   positionType    = GetArchetypeChunkComponentType<Position>(true);

		for(int c = 0; c < chunks.Length; c++)
		{
			ArchetypeChunk chunk = chunks[c];

			NativeArray<Entity> entities 	= chunk.GetNativeArray(entityType);
			NativeArray<Position> positions = chunk.GetNativeArray(positionType);

			for(int e = 0; e < entities.Length; e++)
			{
				Entity entity   = entities[e];
				float3 position = positions[e].Value;

                bool inRadius = !IsOutsideSubMatrix(subMatrix, position);

                DrawBufferType buffer = GetDrawBuffer(subMatrix, position);

                SetDrawBuffer(entity, buffer, commandBuffer);
			}
		}

        commandBuffer.Playback(entityManager);
		commandBuffer.Dispose();

		chunks.Dispose();
    }

    void CheckAllSquares(SubMatrix subMatrix)
    {
        int mapSquareCount = 0;

        EntityCommandBuffer         commandBuffer   = new EntityCommandBuffer(Allocator.Temp);
		NativeArray<ArchetypeChunk> chunks          = allSquaresGroup.CreateArchetypeChunkArray(Allocator.Persistent);

		ArchetypeChunkEntityType                entityType	    = GetArchetypeChunkEntityType();
		ArchetypeChunkComponentType<Position>   positionType    = GetArchetypeChunkComponentType<Position>(true);

		for(int c = 0; c < chunks.Length; c++)
		{
			ArchetypeChunk chunk = chunks[c];

			NativeArray<Entity> entities 	= chunk.GetNativeArray(entityType);
			NativeArray<Position> positions = chunk.GetNativeArray(positionType);

			for(int e = 0; e < entities.Length; e++)
			{
				Entity entity   = entities[e];
				float3 position = positions[e].Value;

                bool inRadius = !IsOutsideSubMatrix(subMatrix, position);

                DrawBufferType buffer = GetDrawBuffer(subMatrix, position);
                UpdateDrawBuffer(entity, buffer, commandBuffer);

                if(!inRadius)
                    RedrawMapSquare(entity, commandBuffer);

                mapSquareCount++;
			}
		}

        commandBuffer.Playback(entityManager);
		commandBuffer.Dispose();

		chunks.Dispose();

        CustomDebugTools.SetDebugText("Total map squares", mapSquareCount);
    }

    public void UpdateDrawBuffer(Entity entity, DrawBufferType buffer, EntityCommandBuffer commandBuffer)
	{
        //DrawBufferType buffer = GetDrawBuffer(worldPosition);

		if(entityManager.HasComponent<Tags.EdgeBuffer>(entity))
        {
            if(buffer == DrawBufferType.EDGE) return;
            commandBuffer.RemoveComponent<Tags.EdgeBuffer>(entity);
        }
        else if(entityManager.HasComponent<Tags.OuterBuffer>(entity))
        {
            if(buffer == DrawBufferType.OUTER) return;
            commandBuffer.RemoveComponent<Tags.OuterBuffer>(entity);
        }
        else if(entityManager.HasComponent<Tags.InnerBuffer>(entity))
        {
            if(buffer == DrawBufferType.INNER) return;
            commandBuffer.RemoveComponent<Tags.InnerBuffer>(entity);
        }

        if(buffer == DrawBufferType.NONE) return;
        else SetDrawBuffer(entity, buffer, commandBuffer);

        CustomDebugTools.HorizontalBufferDebug(entity, (int)buffer);
	}     

    void RedrawMapSquare(Entity entity, EntityCommandBuffer commandBuffer)
    {
        entityUtil.TryRemoveSharedComponent<RenderMesh>(entity, commandBuffer);
        entityUtil.TryAddComponent<Tags.DrawMesh>(entity, commandBuffer);
    }

    public void SetDrawBuffer(Entity entity, DrawBufferType buffer, EntityCommandBuffer commandBuffer)
    {
        //DrawBufferType buffer = GetDrawBuffer(worldPosition);
        switch(buffer)
        {
            case DrawBufferType.INNER:
                commandBuffer.AddComponent<Tags.InnerBuffer>(entity, new Tags.InnerBuffer());
                break;
            case DrawBufferType.OUTER:
                commandBuffer.AddComponent<Tags.OuterBuffer>(entity, new Tags.OuterBuffer());
                break;
            case DrawBufferType.EDGE:
                commandBuffer.AddComponent<Tags.EdgeBuffer>(entity, new Tags.EdgeBuffer());
                break;
            default:
                break;
        }

        CustomDebugTools.HorizontalBufferDebug(entity, (int)buffer);
    }

    /*DrawBufferType GetDrawBuffer(float3 bufferWorldPosition)
    {
        float3 centerPosition = managerSystem.mapMatrix.GridToMatrixPosition(managerSystem.currentMapSquare);
        float3 bufferPosition = managerSystem.mapMatrix.GridToMatrixPosition(bufferWorldPosition);
        int view = TerrainSettings.viewDistance;

        if      (managerSystem.mapMatrix.IsOffsetFromPosition(bufferPosition, centerPosition, view)) return DrawBufferType.EDGE;
        else if (managerSystem.mapMatrix.IsOffsetFromPosition(bufferPosition, centerPosition, view-1)) return DrawBufferType.OUTER;
        else if (managerSystem.mapMatrix.IsOffsetFromPosition(bufferPosition, centerPosition, view-2)) return DrawBufferType.INNER;
        else if (!managerSystem.mapMatrix.InDistancceFromPosition(bufferPosition, centerPosition, view)) return DrawBufferType.EDGE;
        else return DrawBufferType.NONE;
    } */

    SubMatrix LargestSquare(Matrix<Entity> matrix, float3 matrixRootPosition)
	{
		//	Copy original matix to cache so it defaults to original matrix values
		NativeArray<int> cacheMatrix = new NativeArray<int>(matrix.Length, Allocator.Temp);
        for(int i = 0; i < cacheMatrix.Length; i++)
            cacheMatrix[i] = matrix.ItemIsSet(i) ? 1 : 0;

		//	Resulting matrix origin and dimensions
		int resultX = 0;
		int resultZ = 0;
		int resultSize = 0;
		

		for(int x = matrix.width-1; x >= 0; x--)
			for(int z = matrix.width-1; z >= 0; z--)
			{
                int index = matrix.PositionToIndex(new int2(x, z));
				//	At edge, matrix.width-1square size is 1 so default to original matrix
				if(x == matrix.width-1 || z == matrix.width-1) continue;

                int forwardIndex = matrix.PositionToIndex(new int2(x,z+1));
                int rightIndex = matrix.PositionToIndex(new int2(x+1,z));
                int diagonalIndex = matrix.PositionToIndex(new int2(x+1,z+1));


				//	Square is 1, value is equal to 1 + lowed of the three adjacent squares
				if(matrix.ItemIsSet(index)) cacheMatrix[index] = 1 + math.min(cacheMatrix[forwardIndex],
                                                                        math.min(   cacheMatrix[rightIndex],
                                                                                        cacheMatrix[diagonalIndex]));

				//	Largest square so far, store values
				if(cacheMatrix[index] > resultSize)
				{
					resultX = x;
					resultZ = z;
					resultSize = cacheMatrix[index];
				}
			}

        float3 matrixPostiion = new float3(resultX, 0, resultZ);

        float3 squareRootPosition = (matrixPostiion * squareWidth) + matrixRootPosition;

		/*for(int x = 0; x < resultSize; x++)
    		for(int z = 0; z < resultSize; z++)
            {
                
                float3 worldPosition = ((new float3(x, 0, z) + matrixPostiion) * TerrainSettings.mapSquareWidth) + matrixRootPosition;
                CustomDebugTools.Cube(Color.cyan, worldPosition + (squareWidth/2), squareWidth-1);
            } */

        return new SubMatrix(squareRootPosition, resultSize);
        
	}

    SubMatrix TrimSubMatrix(SubMatrix subMatrix)
    {
        int finalWidth = subMatrix.width;
        float3 finalRoot = subMatrix.rootPosition;

        float3 clampRootTo = managerSystem.currentMapSquare - (TerrainSettings.viewDistance * squareWidth);

        int clampWidthTo = TerrainSettings.viewDistance * 2;

        float rootX = clampRootTo.x > finalRoot.x ? clampRootTo.x : finalRoot.x;
        float rootZ = clampRootTo.z > finalRoot.z ? clampRootTo.z : finalRoot.z;

        finalRoot = new float3(rootX, 0, rootZ);

        finalWidth -= (int)math.min(finalRoot.x - subMatrix.rootPosition.x, finalRoot.z - subMatrix.rootPosition.z) / squareWidth;

        if(finalWidth > clampWidthTo) finalWidth = clampWidthTo;

        return new SubMatrix(finalRoot, finalWidth);
    }

    int GetChange(int viewDistance, float boundDistance)
    {
        if(boundDistance > viewDistance)
            return (int)(boundDistance - viewDistance);
        else
            return 0;
    }
}
