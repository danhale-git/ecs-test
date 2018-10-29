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

    public static Vector3 VoxelOwner(Vector3 voxel, int chunkSize)
	{
		int x = Mathf.FloorToInt(voxel.x / chunkSize);
		int y = Mathf.FloorToInt(voxel.y / chunkSize);
		int z = Mathf.FloorToInt(voxel.z / chunkSize);
		return new Vector3(x*chunkSize,y*chunkSize,z*chunkSize);
	}
}
