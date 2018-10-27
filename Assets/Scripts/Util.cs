using System.Collections;
using UnityEngine;
using Unity.Mathematics;

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
    public float3 WrapBlockIndex(float3 index, int chunkSize)
	{
		float[] vector = new float[3] {	index.x,
									    index.y,
									    index.z};

		for(int i = 0; i < 3; i++)
		{
			//	if below min then max
			if(vector[i] == -1) 
				vector[i] = chunkSize-1; 
			//	if above max then min
			else if(vector[i] == chunkSize) 
				vector[i] = 0;
		}

		return new float3(vector[0], vector[1], vector[2]);
	}
    public int WrapAndFlatten(float3 position, int chunkSize)
    {
        return Flatten(WrapBlockIndex(position, chunkSize), chunkSize);
    }
}

public static class Util
{
    public static float3 Unflatten(int index, int xLength, int yLength=0, int zLength=0)
    {
        if(yLength == 0) yLength = xLength;
        if(zLength == 0) zLength = xLength;
        
        int x = index / (xLength * zLength);
        int y = (index - x * yLength * zLength) / zLength;
        int z = index - x * xLength * zLength - y * zLength;

        return new float3(x, y, z);
        //return new float3(z, y, x);
    }
    public static int Flatten(int x, int y, int z, int size)
    {
        return z + size * (y + size * x);
    }
    public static int Flatten(float x, float y, float z, int size)
    {
        return (int)(z + size * (y + size * x));
    }
    public static float3 Unflatten2D(int index, int size)
    {
        int x = index % size;
        int z = index / size;

        return new float3(x, 0, z);
    }
    public static int Flatten2D(int x, int z, int size)
    {
        return (z * size) + x;
    }

    public static float To01(float value)
	{
		return (value * 0.5f) + 0.5f;
	}

    public static Vector3[] CubeVectors()
    {
        return new Vector3[] {  new Vector3( 	-0.5f, -0.5f,	 0.5f ),	//	left front bottom
	                            new Vector3( 	 0.5f, -0.5f,	-0.5f ),	//	right back bottom
	                            new Vector3( 	-0.5f, -0.5f,	-0.5f ), 	//	left back bottom
	                            new Vector3( 	 0.5f, -0.5f,	 0.5f ),	//	right front bottom
	                            new Vector3( 	-0.5f,  0.5f,	 0.5f ),	//	left front top
	                            new Vector3( 	 0.5f,  0.5f,	-0.5f ),	//	right back top
	                            new Vector3( 	-0.5f,  0.5f,	-0.5f ),	//	left back top
	                            new Vector3( 	 0.5f,  0.5f,	 0.5f ) };	//	right front top
    }
}
