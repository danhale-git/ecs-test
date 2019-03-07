using Unity.Mathematics;
using Unity.Collections;
using UnityEngine;

public struct Matrix<T> where T : struct
{
    NativeArray<T> matrix;
    NativeArray<sbyte> isSet;

    public int2 rootPosition;
    public int itemWorldSize;

    public int width;
    public Allocator label;

    public bool initialised{ get{ return matrix.IsCreated; } }

    public int Length{ get{ return matrix.Length; } }

    public void Dispose()
    {
        if(matrix.IsCreated) matrix.Dispose();
        if(isSet.IsCreated) isSet.Dispose();
    }

    public void Initialise(int width, Allocator label, int2 rootPosition, int itemWorldSize)
    {
        Dispose();
        matrix = new NativeArray<T>((int)math.pow(width, 2), label);
        isSet = new NativeArray<sbyte>(matrix.Length, label);
        this.width = width;
        this.label = label;

        this.rootPosition = rootPosition;
        this.itemWorldSize = itemWorldSize;
    }
    public void Initialise()
    {
        Dispose();
        matrix = new NativeArray<T>((int)math.pow(width, 2), label);
        isSet = new NativeArray<sbyte>(matrix.Length, label);
    }

    public float3 ResizeMatrix(int2 matrixPosition)
    {
        int x = (int)matrixPosition.x;
        int z = (int)matrixPosition.y;

        float3 rootPositionChange = float3.zero;
        float3 widthChange = float3.zero;

        if(x < 0)
        {
            int rightGap = EmptyLayersCount(0);
            rootPositionChange.x = x;

            widthChange.x = (x * -1) - rightGap;
            if(widthChange.x < 0) widthChange.x = 0;
            
        }
        else if(x >= width)
        {
            int leftGap = EmptyLayersCount(1);
            widthChange.x = x - (width - 1) - leftGap;
            
            rootPositionChange.x = leftGap;
        }

        if(z < 0)
        {
            int topGap = EmptyLayersCount(2);
            rootPositionChange.z = z;

            widthChange.z = (z * -1) - topGap;
            if(widthChange.z < 0) widthChange.z = 0;
        }
        else if(z >= width)
        {
            int bottomGap = EmptyLayersCount(3);
            widthChange.z = z - (width - 1) - bottomGap;

            rootPositionChange.z = bottomGap;
        }

        int newWidth = width;
        if(widthChange.x+widthChange.z > 0)
            newWidth += math.max((int)widthChange.x, (int)widthChange.z);

        //int2 rootIndexOffset = rootPosition * -1;

        GenerateNewArray(rootPositionChange * -1, newWidth);

        rootPosition += Util.Float3ToInt2(rootPositionChange) * itemWorldSize;

        return rootPositionChange;
    }

    int EmptyLayersCount(int edge)
    {
        int count = 0;

        while(LayerIsEmpty(edge, count) > 0)
            count++;

        return count;
    }

    sbyte LayerIsEmpty(int edge, int offset = 0)
    {
        if(edge > 3) throw new System.Exception("Edge "+edge+" out of range 3");
        
        if(offset >= math.floor(width/2)) return 0;

        if(edge < 2)
        {
            int x       = edge == 0 ? width-1 : 0;
            int xOffset = edge == 0 ? -offset : offset;
            for(int z  = 0; z < width; z++)
                if(ItemIsSet( PositionToIndex(new int2(x+xOffset, z)) ))
                    return 0;
        }
        else
        {
            int z       = edge == 2 ? width-1 : 0;
            int zOffset = edge == 2 ? -offset : offset;
            for(int x  = 0; x < width; x++)
                if(ItemIsSet( PositionToIndex(new int2(x, z+zOffset)) ))
                    return 0;
        }

        return 1;
    }

    public void GenerateNewArray(float3 rootIndexOffset, int newWidth)
    {
        NativeArray<T> newMatrix = new NativeArray<T>((int)math.pow(newWidth, 2), label);
        NativeArray<sbyte> newIsSet = new NativeArray<sbyte>(newMatrix.Length, label);

        for(int i = 0; i < matrix.Length; i++)
        {
            float3 oldMatrixPosition = Util.Unflatten2D(i, width);
            float3 newMatrixPosition = oldMatrixPosition + rootIndexOffset;

            int newIndex = Util.Flatten2D(newMatrixPosition, newWidth);

            if(newIndex < 0 || newIndex >= newMatrix.Length) continue;

            newMatrix[newIndex] = matrix[i];
            newIsSet[newIndex] = isSet[i];
        }

        width = newWidth;
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

    public void UnsetItem(float3 gridPosition)
    {
        UnsetItem(GridPositionToFlatIndex(Util.Float3ToInt2(gridPosition)));
    }

    public void UnsetItem(int2 gridPosition)
    {
        UnsetItem(GridPositionToFlatIndex(gridPosition));
    }

    public void UnsetItem(int index)
    {
        matrix[index] = new T();
        isSet[index] = 0;
    }

    public bool ItemIsSet(int2 gridPosition)
    {
        if(!GridPositionIsInMatrix(gridPosition))
            return false;

        return ItemIsSet(GridPositionToFlatIndex(gridPosition));
    }

    public bool ItemIsSet(int index)
    {
        if(index < 0 || index >= matrix.Length)
            return false;

        return isSet[index] > 0;
    }

    public T GetItem(float3 gridPosition)
    {
		return GetItem(Util.Float3ToInt2(gridPosition));
    }

    public T GetItem(int2 gridPosition)
    {
        int index = GridPositionToFlatIndex(gridPosition);
		return GetItem(index);
    }

    public T GetItem(int index)
    {
        return matrix[index];
    }

    public bool TryGetItem(float3 gridPositionFloat, out T item)
	{
        int2 gridPosition = Util.Float3ToInt2(gridPositionFloat);

        if(!GridPositionIsInMatrix(gridPosition) || !ItemIsSet(gridPosition))
        {
            item = new T();
            return false;
        }

		item = GetItem(GridPositionToFlatIndex(gridPosition));
        return true;
	}

    public bool TryGetItem(int2 gridPosition, out T item)
	{
        if(!GridPositionIsInMatrix(gridPosition) || !ItemIsSet(gridPosition))
        {
            item = new T();
            return false;
        }

		item = GetItem(GridPositionToFlatIndex(gridPosition));
        return true;
	}

    public bool GridPositionIsInMatrix(int2 gridPosition, int offset = 0)
	{
        int2 matrixPosition = GridToMatrixPosition(gridPosition);

        return PositionIsInMatrix(matrixPosition, offset);
	}
    
    public bool PositionIsInMatrix(float3 matrixPosition, int offset = 0)
	{
        return PositionIsInMatrix(new int2((int)matrixPosition.x, (int)matrixPosition.z), offset);
	}
    
    public bool PositionIsInMatrix(int2 matrixPosition, int offset = 0)
	{
        int arrayWidth = width-1;

		if(	matrixPosition.x >= offset && matrixPosition.x <= arrayWidth-offset &&
			matrixPosition.y >= offset && matrixPosition.y <= arrayWidth-offset )
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

    public float3 IndexToPosition(int index)
    {
        return Util.Unflatten2D(index, width);
    }
    public int2 IndexToPositionInt(int index)
    {
        float3 returnValue = Util.Unflatten2D(index, width);
        return new int2((int)returnValue.x, (int)returnValue.z);
    }

    public int GridPositionToFlatIndex(int2 gridPosition)
    {
        return PositionToIndex(GridToMatrixPosition(gridPosition));
    }

    public int2 GridToMatrixPosition(int2 gridPosition)
    {
        return (gridPosition - rootPosition) / itemWorldSize;
    }

    public int PositionToIndex(float3 matrixPosition)
    {
        return Util.Flatten2D(matrixPosition, width);
    }

    public int PositionToIndex(int2 matrixPosition)
    {
        return Util.Flatten2D(matrixPosition.x, matrixPosition.y, width);
    }

    public int2 FlatIndexToGridPosition(int index)
    {
        return MatrixToGridPosition(IndexToPositionInt(index));
    }

    public int2 MatrixToGridPosition(int2 matrixPosition)
    {
        return (matrixPosition * itemWorldSize) + rootPosition;
    }
}
