using Unity.Mathematics;
using Unity.Collections;

public struct Matrix<T> where T : struct
{
    int2 rootPosition;

    NativeArray<T> matrix;
    NativeArray<sbyte> isSet;

    public int width;
    Allocator label;

    public bool initialised{ get{ return matrix.IsCreated; } }

    public int Length{ get{ return matrix.Length; } }

    public void Dispose()
    {
        if(matrix.IsCreated) matrix.Dispose();
        if(isSet.IsCreated) isSet.Dispose();
    }

    public void Initialise(int width, Allocator label)
    {
        Dispose();
        matrix = new NativeArray<T>((int)math.pow(width, 2), label);
        isSet = new NativeArray<sbyte>(matrix.Length, label);
        this.width = width;
        this.label = label;
    }

    NativeArray<T> GetNativeArray()
    {
        return matrix;
    }

    public float3 ResizeMatrix(int2 matrixIndex)
    {
        return ResizeMatrix(new float3(matrixIndex.x, 0, matrixIndex.y));
    }

    public float3 ResizeMatrix(float3 matrixPosition)
    {
        int x = (int)matrixPosition.x;
        int z = (int)matrixPosition.z;

        float3 rootPositionChange = float3.zero;
        float3 widthChange = float3.zero;

        if(x < 0) rootPositionChange.x = x;
        else if(x >= width) widthChange.x = x - (width - 1);

        if(z < 0) rootPositionChange.z = z;
        else if(z >= width) widthChange.z = z - (width - 1);

        float3 rootIndexOffset = rootPositionChange * -1;

        int oldWidth = width;
        widthChange += rootIndexOffset;
        if(widthChange.x+widthChange.z > 0)
            width += math.max((int)widthChange.x, (int)widthChange.z);

        GenerateNewArray(rootIndexOffset, oldWidth);

        return rootPositionChange;
    }

    void GenerateNewArray(float3 rootIndexOffset, int oldWidth)
    {
        NativeArray<T> newMatrix = new NativeArray<T>((int)math.pow(width, 2), label);
        NativeArray<sbyte> newIsSet = new NativeArray<sbyte>(newMatrix.Length, label);

        for(int i = 0; i < matrix.Length; i++)
        {
            float3 oldMatrixPosition = Util.Unflatten2D(i, oldWidth);
            float3 newMatrixPosition = oldMatrixPosition + rootIndexOffset;

            int newIndex = Util.Flatten2D(newMatrixPosition, width);
            newMatrix[newIndex] = matrix[i];
            newIsSet[newIndex] = isSet[i];
        }

        SetMatrix(newMatrix, newIsSet);
    }

    public void SetMatrix(NativeArray<T> newMatrix, NativeArray<sbyte> newIsSet)
    {
        Dispose();
        matrix = newMatrix;
        isSet = newIsSet;
    }

    public void SetItem(T item, int index)
    {
        matrix[index] = item;
        isSet[index] = 1;
    }

    public void UnsetItem(int index)
    {
        matrix[index] = new T();
        isSet[index] = 0;
    }
    
    public bool ItemIsSet(int index)
    {
        return isSet[index] > 0;
    }

    public T GetItem(int index)
    {
        return matrix[index];
    }

    public float3 IndexToPosition(int index)
    {
        return Util.Unflatten2D(index, width);
    }
    
    public int PositionToIndex(float3 matrixPosition)
    {
        return Util.Flatten2D(matrixPosition, width);
    }
    public int PositionToIndex(int2 matrixPosition)
    {
        return Util.Flatten2D(matrixPosition.x, matrixPosition.y, width);
    }
}
