using Unity.Mathematics;
using Unity.Collections;
using UnityEngine;

public struct Matrix<T> where T : struct
{
    NativeArray<T> matrix;
    NativeArray<sbyte> isSet;

    public int width;
    public Allocator label;

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

    public int2 ResizeMatrix(int2 matrixPosition)
    {
        float3 rootPositionChange = ResizeMatrix(new float3(matrixPosition.x, 0, matrixPosition.y));
        return new int2((int)rootPositionChange.x, (int)rootPositionChange.z);
    }

    public float3 ResizeMatrix(float3 matrixPosition)
    {
        
        EmptyEdgesCount(1);
        
        EmptyEdgesCount(3);

        int x = (int)matrixPosition.x;
        int z = (int)matrixPosition.z;

        float3 rootPositionChange = float3.zero;
        float3 widthChange = float3.zero;

        if(x < 0)
        {
            int rightGap = EmptyEdgesCount(0);
            rootPositionChange.x = x;

            widthChange.x = (x * -1) - rightGap;
            if(widthChange.x < 0) widthChange.x = 0;
            
        }
        else if(x >= width)
        {
            widthChange.x = x - (width - 1);
        }

        if(z < 0)
        {
            int topGap = EmptyEdgesCount(2);
            rootPositionChange.z = z;

            widthChange.z = (z * -1) - topGap;
            if(widthChange.z < 0) widthChange.z = 0;
        }
        else if(z >= width)
        {
            widthChange.z = z - (width - 1);
        }

        float3 rootIndexOffset = rootPositionChange * -1;

        //Debug.Log("width "+width);
        //Debug.Log("widthChange "+widthChange);
        //Debug.Log("rootPositionChange "+rootPositionChange);
        //Debug.Log("matrixPosition "+matrixPosition);

        //CountEdges();

        int oldWidth = width;
        if(widthChange.x+widthChange.z > 0)
            width += math.max((int)widthChange.x, (int)widthChange.z);

        GenerateNewArray(rootIndexOffset, oldWidth);

        return rootPositionChange;
    }

    public void CountEdges()
    {
        string debugString = "";

        debugString += "  "+EdgeIsEmpty(2)+'\n';
        debugString += EdgeIsEmpty(1)+"  "+EdgeIsEmpty(0)+'\n';
        debugString += "  "+EdgeIsEmpty(3);

        debugString += "\n-----------\n";

        debugString += "  "+EmptyEdgesCount(2)+'\n';
        debugString += EmptyEdgesCount(1)+"  "+EmptyEdgesCount(0)+'\n';
        debugString += "  "+EmptyEdgesCount(3);

        Debug.Log(debugString);
    }

    int EmptyEdgesCount(int edge)
    {
        int offset = 0;
        int count = 0;

        int safety = 0;

        while(EdgeIsEmpty(edge, offset) > 0 && safety < 100)
        {
            safety++;

            offset++;
            count++;
        }

        return count;
    }

    sbyte EdgeIsEmpty(int edge, int offset = 0)
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

    public void GenerateNewArray(float3 rootIndexOffset, int oldWidth)
    {
        NativeArray<T> newMatrix = new NativeArray<T>((int)math.pow(width, 2), label);
        NativeArray<sbyte> newIsSet = new NativeArray<sbyte>(newMatrix.Length, label);

        for(int i = 0; i < matrix.Length; i++)
        {
            float3 oldMatrixPosition = Util.Unflatten2D(i, oldWidth);
            float3 newMatrixPosition = oldMatrixPosition + rootIndexOffset;

            int newIndex = Util.Flatten2D(newMatrixPosition, width);

            if(newIndex < 0 || newIndex >= newMatrix.Length) continue;

            newMatrix[newIndex] = matrix[i];
            newIsSet[newIndex] = isSet[i];
        }

        SetMatrix(newMatrix, newIsSet);
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
    public int2 IndexToPositionInt(int index)
    {
        float3 returnValue = Util.Unflatten2D(index, width);
        return new int2((int)returnValue.x, (int)returnValue.z);
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
