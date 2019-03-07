using Unity.Mathematics;
using Unity.Collections;

public struct CellMatrix<T> where T : struct
{
    public Matrix<T> array;

    public int Length{ get{ return array.Length; } }

    public void Dispose()
    {
        array.Dispose();
    }

    public void Initialise(int width, Allocator label, int2 rootPosition)
    {
        array.Initialise(width, label, rootPosition); 
    }

    void ResizeMatrices(int2 gridPosition)
    {
        int2 matrixPosition = array.GridToMatrixPosition(gridPosition);
        array.RepositionResize(matrixPosition);
    }

    public void SetItem(T item, int2 gridPosition)
    {
        if(!array.GridPositionIsInMatrix(gridPosition))
            ResizeMatrices(gridPosition);

        int index = array.GridPositionToFlatIndex(gridPosition);
        array.SetItem(item, index);
    }

    
}