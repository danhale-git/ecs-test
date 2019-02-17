using Unity.Mathematics;
using Unity.Collections;

public struct WorldGridMatrix<T> where T : struct
{
    public float3 rootPosition;
    int itemWorldSize;

    NativeArray<T> matrix;
    public int width;

    public WorldGridMatrix(float3 rootPosition, int itemWorldSize, int width, Allocator label)
    {
        this.rootPosition = rootPosition;
        this.itemWorldSize = itemWorldSize;
        matrix = new NativeArray<T>((int)math.pow(width, 2), label);
        this.width = width;
    }
    
    public void Dispose()
    {
        if(matrix.IsCreated)matrix.Dispose();
    }
    public void ReInitialise(Allocator label)
    {
        if(matrix.IsCreated) Dispose();
        matrix = new NativeArray<T>((int)math.pow(width, 2), label);        
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

    public bool TryGetItemFromWorldPosition(float3 worldPosition, out T item)
	{
        float3 localPosition = worldPosition - rootPosition;
        if(!Util.EdgeOverlap(localPosition, width).Equals(float3.zero))
        {
            item = new T();
            return false;
        }

		float3 index = WorldToMatrixPosition(worldPosition);
		item = matrix[Util.Flatten2D(index.x, index.z, width)];
        return true;
	}

    public bool PositionInWorldBounds(float3 worldPosition, int offset = 0)
	{
        float3 index = (worldPosition - rootPosition) / itemWorldSize;
        int arrayWidth = width-1;

		if(	index.x >= offset && index.x <= arrayWidth-offset &&
			index.z >= offset && index.z <= arrayWidth-offset )
			return true;
		else
			return false;
	}

    public bool PositionIsInRing(float3 index, int offset = 0)
	{
        int arrayWidth = width-1;

		if(	index.x == offset ||
            index.z == offset ||
			index.x ==  arrayWidth - offset ||
            index.z ==  arrayWidth - offset )
			return true;
		else
			return false;
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
    public float3 IndexToPosition(int index)
    {
        return Util.Unflatten2D(index, width);
    }
}