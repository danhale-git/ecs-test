using Unity.Mathematics;
using Unity.Collections;

public struct Matrix<T> where T : struct
{
    NativeArray<T> matrix;
    int width;

    public void Dispose()
    {
        if(matrix.IsCreated)matrix.Dispose();
    }

    public void Initialise(int width, Allocator label)
    {
        Dispose();
        matrix = new NativeArray<T>((int)math.pow(width, 2), label); 
    }

    public void SetMatrix(NativeArray<T> newMatrix)
    {
        Dispose();
        matrix = newMatrix;
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

    public float3 IndexToMatrixPosition(int index)
    {
        return Util.Unflatten2D(index, width);
    }
    
    public int MatrixPositionToIndex(float3 matrixPosition)
    {
        return Util.Flatten2D(matrixPosition, width);
    }
}
