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
