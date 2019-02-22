using Unity.Mathematics;
using Unity.Collections;

public struct WorldGridMatrix<T> where T : struct
{
    public float3 rootPosition;
    public int itemWorldSize;

    public NativeArray<T> matrix;
    public int width;
    public Allocator label;

    /*public WorldGridMatrix(float3 rootPosition, int itemWorldSize, int width, Allocator label)
    {
        this.rootPosition = rootPosition;
        this.itemWorldSize = itemWorldSize;
        matrix = new NativeArray<T>((int)math.pow(width, 2), label);
        this.width = width;
        this.label = label;
    } */
    
    public void Dispose()
    {
        if(matrix.IsCreated)matrix.Dispose();
    }

    public void ReInitialise(float3 newRootPosition)
    {
        if(matrix.IsCreated) Dispose();
        matrix = new NativeArray<T>((int)math.pow(width, 2), label);      

        rootPosition = newRootPosition;
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

    public void SetItemFromWorldPosition(T item, float3 worldPosition)
    {
        int index = WorldPositionToIndex(worldPosition);

        SetItem(item, index);
    }
    public T GetItemFromWorldPosition(float3 worldPosition)
    {
		return matrix[WorldPositionToIndex(worldPosition)];
    }

    public void SetItemAndResizeIfNeeded(T item, float3 worldPosition)
    {
        CheckAndResizeMatrix(worldPosition);
        SetItemFromWorldPosition(item, worldPosition);
    }

    public bool TryGetItemFromWorldPosition(float3 worldPosition, out T item)
	{
        if(!WorldPositionIsInMatrix(worldPosition))
        {
            item = new T();
            return false;
        }

		item = matrix[WorldPositionToIndex(worldPosition)];
        return true;
	}

    public bool WorldPositionIsInMatrix(float3 worldPosition, int offset = 0)
	{
        float3 matrixPosition = WorldToMatrixPosition(worldPosition);
        int arrayWidth = width-1;

		if(	matrixPosition.x >= offset && matrixPosition.x <= arrayWidth-offset &&
			matrixPosition.z >= offset && matrixPosition.z <= arrayWidth-offset )
			return true;
		else
			return false;
	}

    public bool IsOffsetFromPosition(float3 isOffsetFrom, float3 position, int offset)
	{
        if(!InDistancceFromPosition(isOffsetFrom, position, offset))
            return false;

		if(	isOffsetFrom.x == position.x - offset ||
            isOffsetFrom.z == position.z - offset ||
			isOffsetFrom.x == position.x + offset ||
            isOffsetFrom.z == position.z + offset )
			return true;
		else
			return false;
	}
    public bool InDistancceFromPosition(float3 inDistanceFrom, float3 position, int offset)
    {
        if(	inDistanceFrom.x >= position.x - offset &&
            inDistanceFrom.z >= position.z - offset &&
			inDistanceFrom.x <= position.x + offset &&
            inDistanceFrom.z <= position.z + offset )
			return true;
		else
			return false;
    }
    public bool InDistanceFromWorldPosition(float3 inDistanceFromWorld, float3 positionWorld, int offset)
    {
        float3 inDistanceFrom = WorldToMatrixPosition(inDistanceFromWorld);
        float3 position = WorldToMatrixPosition(positionWorld);
        return InDistancceFromPosition(inDistanceFrom, position, offset);
    }

    public float3 WorldToMatrixPosition(float3 worldPosition)
    {
        return (worldPosition - rootPosition) / itemWorldSize;
    }
    public float3 MatrixToWorldPosition(float3 matrixPosition)
    {
        return (matrixPosition * itemWorldSize) + rootPosition;
    }
    public int WorldPositionToIndex(float3 worldPosition)
    {
        int index = MatrixPositionToIndex(WorldToMatrixPosition(worldPosition));
        if(index < 0)
        {
            UnityEngine.Debug.Log("index: "+index+"\nwidth: "+width);
            UnityEngine.Debug.Log("WorldToMatrixPosition(worldPosition): "+WorldToMatrixPosition(worldPosition));
            UnityEngine.Debug.Log("worldPosition: "+worldPosition+"\nrootPosition: "+rootPosition);
        }
        return index;
    }
    public float3 IndexToMatrixPosition(int index)
    {
        return Util.Unflatten2D(index, width);
    }
    public int MatrixPositionToIndex(float3 matrixPosition)
    {
        return Util.Flatten2D(matrixPosition, width);
    }

    void CheckAndResizeMatrix(float3 worldPosition)
    {
        if(WorldPositionIsInMatrix(worldPosition))
        {
            UnityEngine.Debug.Log(":: did not resize");
            return;
        }

        UnityEngine.Debug.Log("World position: "+worldPosition+"\nrootPosition: "+rootPosition);

        float3 positionInMatrix = WorldToMatrixPosition(worldPosition);

        int oldWith = width;

        float3 rootPositionChange = CheckAndAdjustBounds(positionInMatrix);

        rootPosition = rootPosition + (rootPositionChange * itemWorldSize);

        NativeArray<T> newMatrix = CreateNewMatrix(rootPositionChange, oldWith);

        matrix.Dispose();
        matrix = newMatrix;

        UnityEngine.Debug.Log(":: width change: "+(width - oldWith)+"\nposition change: "+rootPositionChange);
    }

    float3 CheckAndAdjustBounds(float3 positionInMatrix)
    {
        int x = (int)positionInMatrix.x;
        int z = (int)positionInMatrix.z;

        float3 rootPositionChange = float3.zero;
        float3 widthChange = float3.zero;

        if(x < 0) rootPositionChange.x = x;
        else if(x >= width) widthChange.x = x - (width - 1);

        if(z < 0) rootPositionChange.z = z;
        else if(z >= width) widthChange.z = z - (width - 1);

        widthChange += (rootPositionChange * -1);

        if(widthChange.x+widthChange.z > 0)
            width += math.max((int)widthChange.x, (int)widthChange.z);

        UnityEngine.Debug.Log("rootPositionChange: "+rootPositionChange+"\nwidth change: "+widthChange);

        return rootPositionChange;
    }

    NativeArray<T> CreateNewMatrix(float3 rootPositionChange, int oldWidth)
    {
        NativeArray<T> newMatrix = new NativeArray<T>((int)math.pow(width, 2), label);
        float3 positionOffset = rootPositionChange * -1;

        UnityEngine.Debug.Log("oldWidth: "+ oldWidth+"\nwidth: "+ width);
        UnityEngine.Debug.Log("positionOffset: "+ positionOffset);

        AddOldMatrixWithOffset(positionOffset, oldWidth, newMatrix);

        return newMatrix;
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
    }

}