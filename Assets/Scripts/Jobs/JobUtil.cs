﻿using Unity.Mathematics;

public struct JobUtil
{
    public float3 Unflatten(int index, int xLength, int yLength=0, int zLength=0)
    {
        if(yLength == 0) yLength = xLength;
        if(zLength == 0) zLength = xLength;

        int x = index / (xLength * zLength);
        int y = (index - x * yLength * zLength) / zLength;
        int z = index - x * xLength * zLength - y * zLength;

        return new float3(x, y, z);
    }
    public int Flatten(int x, int y, int z, int size)
    {
        return z + size * (y + size * x);
    }
    public int Flatten(float x, float y, float z, int size)
    {
        return (int)(z + size * (y + size * x));
    }
    public int Flatten(float3 xyz, int size)
    {
        return (int)(xyz.z + size * (xyz.y + size * xyz.x));
    }
    public float3 Unflatten2D(int index, int size)
    {
        int x = index % size;
        int z = index / size;

        return new float3(x, 0, z);
    }
    public int Flatten2D(int x, int z, int size)
    {
        return (z * size) + x;
    }
    public float To01(float value)
	{
		return (value * 0.5f) + 0.5f;
	}
    public int3 WrapBlockIndex(int3 index, int chunkSize)
	{
		int x = index.x;
		int y = index.y;
		int z = index.z;

		if(x == -1) 
			x = chunkSize-1; 
		else if(x == chunkSize) 
			x = 0;

		if(y == -1) 
			y = chunkSize-1; 
		else if(y == chunkSize) 
			y = 0;

		if(z == -1) 
			z = chunkSize-1; 
		else if(z == chunkSize) 
			z = 0;

		return new int3(x, y, z);
	}
    public int WrapAndFlatten(int3 position, int chunkSize)
    {
        return Flatten(WrapBlockIndex(position, chunkSize), chunkSize);
    }
}