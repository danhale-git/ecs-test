using Unity.Mathematics;
using Unity.Collections;

public struct WorldGridMatrix<T> where T : struct
{
    public float3 rootPosition;
    public int itemWorldSize;

    Matrix<T> matrix;
    Matrix<sbyte> bools;

    public int width;
    public Allocator label;
    
    public void Dispose()
    {
        matrix.Dispose();
        bools.Dispose();
    }

    public void ReInitialise(float3 newRootPosition)
    {
        matrix.Initialise(width, label); 
        bools.Initialise(width, label); 

        rootPosition = newRootPosition;
    } 

    public int Length()
    {
        return matrix.Length();
    }

    public void SetItem(T item, int index)
    {
        matrix.SetItem(item, index);
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
        return matrix.GetItem(index);
    }
    public T GetItem(float3 worldPosition)
    {
        int index = WorldPositionToIndex(worldPosition);
		return matrix.GetItem(index);
    }
    public bool TryGetItem(float3 worldPosition, out T item)
	{
        if(!WorldPositionIsInMatrix(worldPosition))
        {
            item = new T();
            return false;
        }

		item = matrix.GetItem(WorldPositionToIndex(worldPosition));
        return true;
	}

    public void SetBool(bool value, int index)
    {
        bools.SetItem(value ? (sbyte)1 : (sbyte)0, index);
    }
    public void SetBool(bool value, float3 worldPosition)
    {
        int index = WorldPositionToIndex(worldPosition);
        bools.SetItem(value ? (sbyte)1 : (sbyte)0, index);
    }

    public bool GetBool(int index)
    {
        return bools.GetItem(index) > 0 ? true : false;
    }
    public bool GetBool(float3 worldPosition)
    {
        int index = WorldPositionToIndex(worldPosition);
        return bools.GetItem(index) > 0 ? true : false;
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

        float3 rootPositionChange = CheckAndAdjustBounds(positionInMatrix);

        //  move the checkandadjustbounds
        rootPosition = rootPosition + (rootPositionChange * itemWorldSize);

        matrix.AdjustMatrixSize(rootPositionChange, width);
        bools.AdjustMatrixSize(rootPositionChange, width);
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
}