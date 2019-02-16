using Unity.Mathematics;
using Unity.Collections;

struct Matrix<T> where T : struct
{
    float3 rootPosition;
    int itemWorldSize;

    NativeArray<T> matrix;
    int width;

    public Matrix(float3 rootPosition, int itemWorldSize, int width, Allocator label)
    {
        this.rootPosition = rootPosition;
        this.itemWorldSize = itemWorldSize;
        matrix = new NativeArray<T>((int)math.pow(width, 2), label);
        this.width = width;
    }
    
    public void Dispose()
    {
        matrix.Dispose();
    }

    public void Add(T item, int index)
    {
        matrix[index] = item;
    }

    public void AddFromWorldPosition(T item, float3 worldPosition)
    {
        int index = Util.Flatten2D(WorldToMatrixPosition(worldPosition), width);
        Add(item, index);
    }

    public T GetFromWorldPosition(float3 worldPosition)
    {
        float3 index = WorldToMatrixPosition(worldPosition);
		return matrix[Util.Flatten2D(index.x, index.z, width)];
    }

    public bool TryGetFromWorldPosition(float3 worldPosition, out T item)
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

    bool PositionInMatrixWorldBounds(float3 worldPosition, float3 matrixRootPosition, int offset = 0)
	{
        float3 index = (worldPosition - matrixRootPosition) / itemWorldSize;
        int arrayWidth = width-1;

		if(	index.x >= offset && index.x <= arrayWidth-offset &&
			index.z >= offset && index.z <= arrayWidth-offset )
			return true;
		else
			return false;
	}

    public bool SquareInRing(float3 index, int offset = 0)
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

    float3 WorldToMatrixPosition(float3 worldPosition)
    {
        return (worldPosition - rootPosition) / itemWorldSize;
    }
}