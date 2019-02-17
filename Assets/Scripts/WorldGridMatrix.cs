using Unity.Mathematics;
using Unity.Collections;

public struct WorldGridMatrix<T> where T : struct
{
    public float3 rootPosition;
    int itemWorldSize;

    NativeArray<T> matrix;
    public int width;
    Allocator label;

    public WorldGridMatrix(float3 rootPosition, int itemWorldSize, int width, Allocator label)
    {
        this.rootPosition = rootPosition;
        this.itemWorldSize = itemWorldSize;
        matrix = new NativeArray<T>((int)math.pow(width, 2), label);
        this.width = width;
        this.label = label;
    }
    
    public void Dispose()
    {
        if(matrix.IsCreated)matrix.Dispose();
    }

    public void ReInitialise(float3 newRootPosition)
    {
        if(matrix.IsCreated) Dispose();
        matrix = new NativeArray<T>((int)math.pow(width, 2), label);      

        rootPosition = newRootPosition;
    } 

    public int Length()
    {
        return matrix.Length;
    }

    public void SetItem(T item, int index)
    {
        matrix[index] = item;
    }
    public T GetItem(int index)
    {
        return matrix[index];
    }

    public void SetItemFromWorldPosition(T item, float3 worldPosition)
    {
        int index = WorldPositionToIndex(worldPosition);
        SetItem(item, index);
    }
    public T GetItemFromWorldPosition(float3 worldPosition)
    {
		return matrix[WorldPositionToIndex(worldPosition)];
    }

    public void SetItemAndResizeIfNeeded(T item, float3 worldPosition)
    {
        CheckAndResizeMatrix(worldPosition);
        SetItemFromWorldPosition(item, worldPosition);
    }

    public bool TryGetItemFromWorldPosition(float3 worldPosition, out T item)
	{
        float3 localPosition = worldPosition - rootPosition;
        if(!Util.EdgeOverlap(localPosition, width).Equals(float3.zero))
        {
            item = new T();
            return false;
        }

		item = matrix[WorldPositionToIndex(worldPosition)];
        return true;
	}

    public bool WorldPositionIsInMatrix(float3 worldPosition, int offset = 0)
	{
        float3 matrixPosition = WorldToMatrixPosition(worldPosition);
        int arrayWidth = width-1;

		if(	matrixPosition.x >= offset && matrixPosition.x <= arrayWidth-offset &&
			matrixPosition.z >= offset && matrixPosition.z <= arrayWidth-offset )
			return true;
		else
			return false;
	}

    public bool IsOffsetFromPosition(float3 isOffsetFrom, float3 position, int offset)
	{
        if(!InDistancceFromPosition(isOffsetFrom, position, offset))
            return false;

		if(	isOffsetFrom.x == position.x - offset ||
            isOffsetFrom.z == position.z - offset ||
			isOffsetFrom.x == position.x + offset ||
            isOffsetFrom.z == position.z + offset )
			return true;
		else
			return false;
	}
    public bool InDistancceFromPosition(float3 inDistanceFrom, float3 position, int offset)
    {
        if(	inDistanceFrom.x >= position.x - offset &&
            inDistanceFrom.z >= position.z - offset &&
			inDistanceFrom.x <= position.x + offset &&
            inDistanceFrom.z <= position.z + offset )
			return true;
		else
			return false;
    }
    public bool InDistanceFromWorldPosition(float3 inDistanceFromWorld, float3 positionWorld, int offset)
    {
        float3 inDistanceFrom = WorldToMatrixPosition(inDistanceFromWorld);
        float3 position = WorldToMatrixPosition(positionWorld);
        return InDistancceFromPosition(inDistanceFrom, position, offset);
    }

    public float3 WorldToMatrixPosition(float3 worldPosition)
    {
        return (worldPosition - rootPosition) / itemWorldSize;
    }
    public float3 MatrixToWorldPosition(float3 matrixPosition)
    {
        return (matrixPosition * itemWorldSize) + rootPosition;
    }
    public int WorldPositionToIndex(float3 worldPosition)
    {
        return Util.Flatten2D(WorldToMatrixPosition(worldPosition), width);
    }
    public float3 IndexToMatrixPosition(int index)
    {
        return Util.Unflatten2D(index, width);
    }

    void CheckAndResizeMatrix(float3 worldPosition)
    {
        if(WorldPositionIsInMatrix(worldPosition))
            return;

        float3 positionInMatrix = WorldToMatrixPosition(worldPosition);

        float3 rootPositionChange = CheckAndAdjustBounds(positionInMatrix);

        NativeArray<T> newMatrix = CreateNewMatrix(rootPositionChange);

        matrix.Dispose();
        matrix = newMatrix;
    }

    float3 CheckAndAdjustBounds(float3 positionInMatrix)
    {
        int x = (int)positionInMatrix.x;
        int z = (int)positionInMatrix.z;

        float3 rootPositionChange = float3.zero;

        int xWidth = 0;
        int zWidth = 0;

        if(x < 0) rootPositionChange.x = x;
        else if(x >= width) xWidth = x - (width - 1);

        if(z < 0) rootPositionChange.z = z;
        else if(z >= width) zWidth = z - (width - 1);

        if(xWidth+zWidth > 0)
            width += math.max(xWidth, zWidth);

        return rootPositionChange;
    }

    NativeArray<T> CreateNewMatrix(float3 rootPositionChange)
    {
        NativeArray<T> newMatrix = new NativeArray<T>((int)math.pow(width, 2), label);
        float3 positionOffset = rootPositionChange * -1;

        AddOldMatrixWithOffset(positionOffset, newMatrix);

        return newMatrix;
    }

    void AddOldMatrixWithOffset(float3 positionChange, NativeArray<T> newMatrix)
    {
        for(int i = 0; i < matrix.Length; i++)
        {
            float3 oldMatrixPosition = IndexToMatrixPosition(i);
            float3 newMatrixPosition = oldMatrixPosition + positionChange;

            int newMatrixIndex = Util.Flatten2D(newMatrixPosition, width);
            newMatrix[newMatrixIndex] = matrix[i];
        }
    }

}