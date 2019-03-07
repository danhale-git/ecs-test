using Unity.Mathematics;
using Unity.Collections;

public struct MapMatrix<T> where T : struct
{
    public float3 rootPosition;
    public int gridSquareSize;

    Matrix<T> matrix;
    Matrix<sbyte> discovered;

    public int Length{ get{ return matrix.Length; } }

    public Matrix<T> GetMatrix()
    {
        return matrix;
    }

    public void Dispose()
    {
        matrix.Dispose();
        discovered.Dispose();
    }

    public void Initialise(int width, Allocator label)
    {
        matrix.Initialise(width, label); 
        discovered.Initialise(width, label); 
    }

    public void ClearDiscoveryStatus()
    {
        discovered.Dispose();
        discovered.Initialise(matrix.width, matrix.label); 
    }

    void ResizeMatrices(float3 gridPosition)
    {
        float3 matrixPosition = GridToMatrixPosition(gridPosition);
        float3 rootPositionChange = matrix.ResizeMatrix(matrixPosition);

        int newWidth = matrix.width;
        discovered.GenerateNewArray(rootPositionChange * -1, newWidth);

        rootPosition = rootPosition + (rootPositionChange * gridSquareSize);
    }

    public void SetItem(T item, float3 gridPosition)
    {
        if(!GridPositionIsInMatrix(gridPosition))
            ResizeMatrices(gridPosition);

        int index = GridPositionToFlatIndex(gridPosition);
        matrix.SetItem(item, index);
    }

    public void UnsetItem(float3 gridPosition)
    {
        matrix.UnsetItem(GridPositionToFlatIndex(gridPosition));
    }

    public bool ItemIsSet(float3 gridPosition)
    {
        if(!GridPositionIsInMatrix(gridPosition))
            return false;

        return matrix.ItemIsSet(GridPositionToFlatIndex(gridPosition));
    }
    public bool ItemIsSet(int index)
    {
        if(index < 0 || index >= matrix.Length)
            return false;

        return matrix.ItemIsSet(index);
    }

    public T GetItem(int index)
    {
		return matrix.GetItem(index);
    }

    public T GetItem(float3 gridPosition)
    {
        int index = GridPositionToFlatIndex(gridPosition);
		return matrix.GetItem(index);
    }

    public bool TryGetItem(float3 gridPosition, out T item)
	{
        if(!GridPositionIsInMatrix(gridPosition) || !ItemIsSet(gridPosition))
        {
            item = new T();
            return false;
        }

		item = matrix.GetItem(GridPositionToFlatIndex(gridPosition));
        return true;
	}

    public void SetAsDiscovered(bool value, float3 gridPosition)
    {
        int index = GridPositionToFlatIndex(gridPosition);
        discovered.SetItem(value ? (sbyte)1 : (sbyte)0, index);
    }

    public void SetAsDiscovered(bool value, int index)
    {
        discovered.SetItem(value ? (sbyte)1 : (sbyte)0, index);
    }

    public bool SquareIsDiscovered(float3 gridPosition)
    {
        if(!GridPositionIsInMatrix(gridPosition))
            return false;
            
        int index = GridPositionToFlatIndex(gridPosition);
        return discovered.GetItem(index) > 0 ? true : false;
    }

    public bool SquareIsDiscovered(int index)
    {
        if(index < 0 || index >= matrix.Length)
            return false;
            
        return discovered.GetItem(index) > 0 ? true : false;
    }

    public bool GridPositionIsInMatrix(float3 gridPosition, int offset = 0)
	{
        float3 matrixPosition = GridToMatrixPosition(gridPosition);

        return matrix.PositionIsInMatrix(matrixPosition, offset);
	}
    
    public int GridPositionToFlatIndex(float3 gridPosition)
    {
        return matrix.PositionToIndex(GridToMatrixPosition(gridPosition));
    }

    public float3 GridToMatrixPosition(float3 gridPosition)
    {
        return (gridPosition - rootPosition) / gridSquareSize;
    }
    
    public float3 FlatIndexToGridPosition(int index)
    {
        return MatrixToGridPosition(matrix.IndexToPosition(index));
    }
    public float3 MatrixToGridPosition(float3 matrixPosition)
    {
        return (matrixPosition * gridSquareSize) + rootPosition;
    }
}