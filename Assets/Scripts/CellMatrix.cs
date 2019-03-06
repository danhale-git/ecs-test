using Unity.Mathematics;
using Unity.Collections;

public struct CellMatrix<T> where T : struct
{
    public int2 rootPosition;

    Matrix<T> matrix;

    public int Length{ get{ return matrix.Length; } }

    public Matrix<T> GetMatrix()
    {
        return matrix;
    }

    public void Dispose()
    {
        matrix.Dispose();
    }

    public void Initialise(int width, Allocator label)
    {
        matrix.Initialise(width, label); 
    }

    void ResizeMatrices(int2 gridPosition)
    {
        int2 matrixPosition = GridToMatrixPosition(gridPosition);
        int2 rootPositionChange = matrix.ResizeMatrix(matrixPosition);

        rootPosition = rootPosition + rootPositionChange;
    }

    public void SetItem(T item, int2 gridPosition)
    {
        if(!GridPositionIsInMatrix(gridPosition))
            ResizeMatrices(gridPosition);

        int index = GridPositionToFlatIndex(gridPosition);
        matrix.SetItem(item, index);
    }

    public void UnsetItem(int2 gridPosition)
    {
        matrix.UnsetItem(GridPositionToFlatIndex(gridPosition));
    }

    public bool ItemIsSet(int2 gridPosition)
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

    public T GetItem(int2 gridPosition)
    {
        int index = GridPositionToFlatIndex(gridPosition);
		return matrix.GetItem(index);
    }

    public bool TryGetItem(int2 gridPosition, out T item)
	{
        if(!GridPositionIsInMatrix(gridPosition) || !ItemIsSet(gridPosition))
        {
            item = new T();
            return false;
        }

		item = matrix.GetItem(GridPositionToFlatIndex(gridPosition));
        return true;
	} 

    public bool GridPositionIsInMatrix(int2 gridPosition, int offset = 0)
	{
        int2 matrixPosition = GridToMatrixPosition(gridPosition);

        return matrix.PositionIsInMatrix(matrixPosition, offset);
	}

    public bool InDistancceFromPosition(int2 inDistanceFrom, int2 position, int offset)
    {
        if(	inDistanceFrom.x >= position.x - offset &&
            inDistanceFrom.y >= position.y - offset &&
			inDistanceFrom.x <= position.x + offset &&
            inDistanceFrom.y <= position.y + offset )
			return true;
		else
			return false;
    }
    public bool InDistanceFromGridPosition(int2 inDistanceFromGrid, int2 positionGrid, int offset)
    {
        int2 inDistanceFrom = GridToMatrixPosition(inDistanceFromGrid);
        int2 position = GridToMatrixPosition(positionGrid);
        return InDistancceFromPosition(inDistanceFrom, position, offset);
    } 
    
    public int GridPositionToFlatIndex(int2 gridPosition)
    {
        return matrix.PositionToIndex(GridToMatrixPosition(gridPosition));
    }

    public int2 GridToMatrixPosition(int2 gridPosition)
    {
        return gridPosition - rootPosition;
    }
    
    public int2 FlatIndexToGridPosition(int index)
    {
        return MatrixToGridPosition(matrix.IndexToPositionInt(index));
    }
    public int2 MatrixToGridPosition(int2 matrixPosition)
    {
        return matrixPosition + rootPosition;
    }
}