using Unity.Mathematics;
using Unity.Collections;

public struct WorldGridMatrix<T> where T : struct
{
    public float3 rootPosition;
    public int itemWorldSize;

    Matrix<T> matrix;
    Matrix<sbyte> bools;

    public void Dispose()
    {
        matrix.Dispose();
        bools.Dispose();
    }

    public void ReInitialise(float3 newRootPosition, int width, Allocator label)
    {
        matrix.Initialise(width, label); 
        bools.Initialise(width, label); 

        rootPosition = newRootPosition;
    } 

    public int Length()
    {
        return matrix.Length();
    }

    public void SetItem(T item, float3 worldPosition)
    {
        if(!WorldPositionIsInMatrix(worldPosition))
            ResizeMatrix(worldPosition);

        int index = WorldPositionToIndex(worldPosition);
        matrix.SetItem(item, index);
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

    public void SetBool(bool value, float3 worldPosition)
    {
        int index = WorldPositionToIndex(worldPosition);
        bools.SetItem(value ? (sbyte)1 : (sbyte)0, index);
    }

    public bool GetBool(float3 worldPosition)
    {
        int index = WorldPositionToIndex(worldPosition);
        return bools.GetItem(index) > 0 ? true : false;
    }

    public bool WorldPositionIsInMatrix(float3 worldPosition, int offset = 0)
	{
        float3 matrixPosition = WorldToMatrixPosition(worldPosition);
        int arrayWidth = matrix.width-1;

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
        return Util.Unflatten2D(index, matrix.width);
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
        return Util.Flatten2D(matrixPosition, matrix.width);
    }
    public int WorldPositionToIndex(float3 worldPosition)
    {
        return MatrixPositionToIndex(WorldToMatrixPosition(worldPosition));
    }

    void ResizeMatrix(float3 worldPosition)
    {
        float3 positionInMatrix = WorldToMatrixPosition(worldPosition);

        int width = matrix.width;

        int x = (int)positionInMatrix.x;
        int z = (int)positionInMatrix.z;

        float3 rootPositionChange = float3.zero;
        float3 widthChange = float3.zero;

        if(x < 0) rootPositionChange.x = x;
        else if(x >= width) widthChange.x = x - (width - 1);

        if(z < 0) rootPositionChange.z = z;
        else if(z >= width) widthChange.z = z - (width - 1);

        float3 rootIndexOffset = rootPositionChange * -1;
        this.rootPosition = rootPosition + (rootPositionChange * itemWorldSize);

        widthChange += rootIndexOffset;
        if(widthChange.x+widthChange.z > 0)
            width += math.max((int)widthChange.x, (int)widthChange.z);

        matrix.AdjustMatrixSize(rootIndexOffset, width);
        bools.AdjustMatrixSize(rootIndexOffset, width);
    }
}