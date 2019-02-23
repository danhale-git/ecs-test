using Unity.Mathematics;
using Unity.Collections;

public struct Matrix<T> where T : struct
{
    NativeArray<T> matrix;
    int width;
    Allocator label;

    public void Dispose()
    {
        if(matrix.IsCreated)matrix.Dispose();
    }

    public void Initialise(int width, Allocator label)
    {
        Dispose();
        matrix = new NativeArray<T>((int)math.pow(width, 2), label);
        this.width = width;
        this.label = label;
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

    public void AdjustMatrixSize(float3 rootPositionChange, int newWidth)
    {
        int oldWidth = width;
        width = newWidth;

        NativeArray<T> newMatrix = new NativeArray<T>((int)math.pow(width, 2), label);
        float3 positionOffset = rootPositionChange * -1;

        AddOldMatrixWithOffset(positionOffset, oldWidth, newMatrix);
    }

    void AddOldMatrixWithOffset(float3 positionOffset, int oldWidth, NativeArray<T> newMatrix)
    {
        for(int i = 0; i < matrix.Length; i++)
        {
            float3 oldMatrixPosition = Util.Unflatten2D(i, oldWidth);
            float3 newMatrixPosition = oldMatrixPosition + positionOffset;

            int newMatrixIndex = Util.Flatten2D(newMatrixPosition, width);
            newMatrix[newMatrixIndex] = matrix[i];
        }

        SetMatrix(newMatrix);
    }
}
