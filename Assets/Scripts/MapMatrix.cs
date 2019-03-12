using Unity.Mathematics;
using Unity.Collections;

public struct MapMatrix<T> where T : struct
{
    public Matrix<T> array;
    Matrix<sbyte> discoveredArray;

    public int Length{ get{ return array.Length; } }

    public Matrix<T> GetMatrix()
    {
        return array;
    }

    public void Dispose()
    {
        array.Dispose();
        discoveredArray.Dispose();
    }

    public void Initialise(int width, Allocator label, float3 rootPosition, int itemWorldSize)
    {
        int2 root = Util.Float3ToInt2(rootPosition);
        array.Initialise(width, label, root, itemWorldSize); 
        discoveredArray.Initialise(width, label, root, itemWorldSize); 
    }

    public void ClearDiscoveredSquares()
    {
        discoveredArray.Dispose();
        discoveredArray.Initialise(); 
    }

    void ResizeMatrices(float3 gridPosition)
    {
        int2 matrixPosition = array.GridToMatrixPosition(Util.Float3ToInt2(gridPosition));
        int2 rootIndexOffset = array.RepositionResize(matrixPosition);

        int newWidth = array.width;
        discoveredArray.CopyToAdjustedMatrix(rootIndexOffset, newWidth);
    }

    public void SetItem(T item, float3 gridPosition)
    {
        if(!array.GridPositionIsInMatrix(Util.Float3ToInt2(gridPosition)))
            ResizeMatrices(gridPosition);

        int index = array.GridPositionToFlatIndex(Util.Float3ToInt2(gridPosition));
        array.SetItem(item, index);
    }

    public void SetAsDiscovered(bool value, float3 gridPosition)
    {
        int index = array.GridPositionToFlatIndex(Util.Float3ToInt2(gridPosition));
        discoveredArray.SetItem(value ? (sbyte)1 : (sbyte)0, index);
    }

    public void SetAsDiscovered(bool value, int index)
    {
        discoveredArray.SetItem(value ? (sbyte)1 : (sbyte)0, index);
    }

    public bool SquareIsDiscovered(float3 gridPosition)
    {
        if(!array.GridPositionIsInMatrix(Util.Float3ToInt2(gridPosition)))
            return false;
            
        int index = array.GridPositionToFlatIndex(Util.Float3ToInt2(gridPosition));
        return discoveredArray.GetItem(index) > 0 ? true : false;
    }

    public bool SquareIsDiscovered(int index)
    {
        if(index < 0 || index >= array.Length)
            return false;
            
        return discoveredArray.GetItem(index) > 0 ? true : false;
    }

}