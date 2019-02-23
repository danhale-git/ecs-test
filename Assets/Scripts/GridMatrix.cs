using Unity.Mathematics;
using Unity.Collections;

public struct GridMatrix<T> where T : struct
{
    Matrix<T> matrix;
    Matrix<sbyte> bools;

    public int Length{ get{ return matrix.Length; } }

    public void Dispose()
    {
        matrix.Dispose();
        bools.Dispose();
    }

    public void ReInitialise(float3 newRootPosition, int width, Allocator label)
    {
        matrix.Initialise(width, label); 
        bools.Initialise(width, label); 
    } 

    void ResizeMatrices(float3 matrixPosition)
    {
        matrix.ResizeMatrix(matrixPosition);
        bools.ResizeMatrix(matrixPosition);
    }

    public void SetItem(T item, float3 matrixPosition)
    {
        if(!PositionIsInMatrix(matrixPosition))
            ResizeMatrices(matrixPosition);

        int index = matrix.PositionToIndex(matrixPosition);
        matrix.SetItem(item, index);
    }

    public T GetItem(float3 matrixPosition)
    {
        int index = matrix.PositionToIndex(matrixPosition);
		return matrix.GetItem(index);
    }

    public bool TryGetItem(float3 matrixPosition, out T item)
	{
        if(!PositionIsInMatrix(matrixPosition))
        {
            item = new T();
            return false;
        }

		item = matrix.GetItem(matrix.PositionToIndex(matrixPosition));
        return true;
	}

    public void SetBool(bool value, float3 matrixPosition)
    {
        int index = matrix.PositionToIndex(matrixPosition);
        bools.SetItem(value ? (sbyte)1 : (sbyte)0, index);
    }

    public bool GetBool(float3 matrixPosition)
    {
        int index = matrix.PositionToIndex(matrixPosition);
        return bools.GetItem(index) > 0 ? true : false;
    }

    public bool PositionIsInMatrix(float3 matrixPosition, int offset = 0)
	{
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
}