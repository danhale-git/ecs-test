using System.Collections;
using UnityEngine;
using Unity.Mathematics;

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
    public static int Flatten2D(float x, float z, int size)
    {
        return ((int)z * size) + (int)x;
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

    public static Vector3[] CubeVectorsPointFiveOffset()
    {
        return new Vector3[] {  new Vector3(0, 0, 1),	//	left front bottom
	                            new Vector3(1, 0, 0),	//	right back bottom
	                            new Vector3(0, 0, 0), 	//	left back bottom
	                            new Vector3(1, 0, 1),	//	right front bottom
	                            new Vector3(0, 1, 1),	//	left front top
	                            new Vector3(1, 1, 0),	//	right back top
	                            new Vector3(0, 1, 0),	//	left back top
	                            new Vector3(1, 1, 1) };	//	right front top
    }

    public static Vector3 VoxelOwner(Vector3 voxel, int cubeSize)
	{
		int x = Mathf.FloorToInt(voxel.x / cubeSize);
		int y = Mathf.FloorToInt(voxel.y / cubeSize);
		int z = Mathf.FloorToInt(voxel.z / cubeSize);
		return new Vector3(x*cubeSize,y*cubeSize,z*cubeSize);
	}
}
