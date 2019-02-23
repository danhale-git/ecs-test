using Unity.Mathematics;
using Unity.Collections;

public struct WorldGridMatrix<T> where T : struct
{
    public float3 rootPosition;
    public int itemWorldSize;

    NativeArray<T> matrix;
    NativeArray<sbyte> bools;

    public int width;
    public Allocator label;
    
    public void Dispose()
    {
        if(matrix.IsCreated)matrix.Dispose();
        if(bools.IsCreated)bools.Dispose();
    }

    public void ReInitialise(float3 newRootPosition)
    {
        if(matrix.IsCreated) Dispose();
        matrix = new NativeArray<T>((int)math.pow(width, 2), label); 
        bools = new NativeArray<sbyte>(matrix.Length, label);   

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
    public void SetItem(T item, float3 worldPosition)
    {
        int index = WorldPositionToIndex(worldPosition);

        SetItem(item, index);
    }
    public void SetItemAndResizeIfNeeded(T item, float3 worldPosition)
    {
        CheckAndResizeMatrix(worldPosition);
        SetItem(item, worldPosition);
    }

    public T GetItem(int index)
    {
        return matrix[index];
    }
    public T GetItem(float3 worldPosition)
    {
        int index = WorldPositionToIndex(worldPosition);
		return matrix[index];
    }
    public bool TryGetItem(float3 worldPosition, out T item)
	{
        if(!WorldPositionIsInMatrix(worldPosition))
        {
            item = new T();
            return false;
        }

		item = matrix[WorldPositionToIndex(worldPosition)];
        return true;
	}

    public void SetBool(bool value, int index)
    {
        bools[index] = value ? (sbyte)1 : (sbyte)0;
    }
    public void SetBool(bool value, float3 worldPosition)
    {
        int index = WorldPositionToIndex(worldPosition);
        bools[index] = value ? (sbyte)1 : (sbyte)0;
    }

    public bool GetBool(int index)
    {
        return bools[index] > 0 ? true : false;
    }
    public bool GetBool(float3 worldPosition)
    {
        int index = WorldPositionToIndex(worldPosition);
        return bools[index] > 0 ? true : false;
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

    public float3 IndexToMatrixPosition(int index)
    {
        return Util.Unflatten2D(index, width);
    }
    public float3 WorldToMatrixPosition(float3 worldPosition)
    {
        return (worldPosition - rootPosition) / itemWorldSize;
    }
    
    public float3 IndexToWorldPosition(int index)
    {
        return MatrixToWorldPosition(IndexToMatrixPosition(index));
    }
    public float3 MatrixToWorldPosition(float3 matrixPosition)
    {
        return (matrixPosition * itemWorldSize) + rootPosition;
    }
    
    public int MatrixPositionToIndex(float3 matrixPosition)
    {
        return Util.Flatten2D(matrixPosition, width);
    }
    public int WorldPositionToIndex(float3 worldPosition)
    {
        return MatrixPositionToIndex(WorldToMatrixPosition(worldPosition));
    }

    void CheckAndResizeMatrix(float3 worldPosition)
    {
        if(WorldPositionIsInMatrix(worldPosition))
        {
            return;
        }

        float3 positionInMatrix = WorldToMatrixPosition(worldPosition);

        int oldWith = width;

        float3 rootPositionChange = CheckAndAdjustBounds(positionInMatrix);

        rootPosition = rootPosition + (rootPositionChange * itemWorldSize);

        NativeArray<T> newMatrix = CreateNewMatrix(rootPositionChange, oldWith);

        
    }

    float3 CheckAndAdjustBounds(float3 positionInMatrix)
    {
        int x = (int)positionInMatrix.x;
        int z = (int)positionInMatrix.z;

        float3 rootPositionChange = float3.zero;
        float3 widthChange = float3.zero;

        if(x < 0) rootPositionChange.x = x;
        else if(x >= width) widthChange.x = x - (width - 1);

        if(z < 0) rootPositionChange.z = z;
        else if(z >= width) widthChange.z = z - (width - 1);

        widthChange += (rootPositionChange * -1);

        if(widthChange.x+widthChange.z > 0)
            width += math.max((int)widthChange.x, (int)widthChange.z);

        return rootPositionChange;
    }

    NativeArray<T> CreateNewMatrix(float3 rootPositionChange, int oldWidth)
    {
        NativeArray<T> newMatrix = new NativeArray<T>((int)math.pow(width, 2), label);
        NativeArray<sbyte> newBools = new NativeArray<sbyte>((int)math.pow(width, 2), label);
        float3 positionOffset = rootPositionChange * -1;

        AddOldMatrixWithOffset(positionOffset, oldWidth, newMatrix, newBools);

        return newMatrix;
    }

    void AddOldMatrixWithOffset(float3 positionOffset, int oldWidth, NativeArray<T> newMatrix, NativeArray<sbyte> newBools)
    {
        for(int i = 0; i < matrix.Length; i++)
        {
            float3 oldMatrixPosition = Util.Unflatten2D(i, oldWidth);
            float3 newMatrixPosition = oldMatrixPosition + positionOffset;


            int newMatrixIndex = Util.Flatten2D(newMatrixPosition, width);
            newMatrix[newMatrixIndex] = matrix[i];
            newBools[newMatrixIndex] = bools[i];
        }

        matrix.Dispose();
        matrix = newMatrix;

        bools.Dispose();
        bools = newBools;
    }
}