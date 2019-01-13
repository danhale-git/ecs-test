using System.Collections;
using UnityEngine;
using Unity.Mathematics;
using MyComponents;

public static class Util
{
    public static int Flatten(int x, int y, int z, int width)
    {
        return ((z * width) + x) + (y * (width * width));
    }
    public static int Flatten(float x, float y, float z, int width)
    {
        return (((int)z * width) + (int)x) + ((int)y * (width * width));
    }

    public static float3 Unflatten(int index, int width)
    {
        int y = (int)math.floor(index / (width * width));
        index -= y * (width * width);
        int z = (int)math.floor(index / width);
        int x = index - (width * z);
        return new float3(x, y, z);
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

    public static float3 Float3Floor(float3 value)
    {
        return new float3(
            math.floor(value.x),
            math.floor(value.y),
            math.floor(value.z)
        );
    }

    public static Vector3 VoxelOwner(Vector3 position, int cubeSize)
	{
		int x = Mathf.FloorToInt(position.x / cubeSize);
		int z = Mathf.FloorToInt(position.z / cubeSize);
		return new Vector3(x*cubeSize, 0, z*cubeSize);
	}

    public static Vector3 LocalVoxel(Vector3 position, int cubeSize, bool debug = false)
	{
		float3 ownerWorldPosition = VoxelOwner(position, cubeSize);
        return Float3Floor(position) - ownerWorldPosition;
	}

    public static float3[] CardinalDirections()
    {
        return new float3[8] {
			new float3( 1,  0,  0), //  0  right
			new float3(-1,  0,  0), //  1  left    
			new float3( 0,  0,  1), //  2  front
			new float3( 0,  0, -1), //  3  back
			new float3( 1,  0,  1), //  4  front right
			new float3(-1,  0,  1), //  5  front left
			new float3( 1,  0, -1), //  6  back right
			new float3(-1,  0, -1)	//  7  back left
		    };
    }
    public static int DirectionToIndex(float3 direction)
    {
        float x = direction.x;
        float z = direction.z;

        if(x > 0 && z == 0) return 0;
        if(x < 0 && z == 0) return 1;
        if(x == 0 && z > 0) return 2;
        if(x == 0 && z < 0) return 3;
        if(x > 0 && z > 0) return 4;
        if(x < 0 && z > 0) return 5;
        if(x > 0 && z < 0) return 6;
        if(x < 0 && z < 0) return 7;
        return 0;
    }
    
    public static int WrapAndFlatten2D(int x, int z, int chunkSize)
    {
        if(x == -1) 
			x = chunkSize-1; 
		else if(x == chunkSize) 
			x = 0;

		if(z == -1) 
			z = chunkSize-1; 
		else if(z == chunkSize) 
			z = 0;

        return Flatten2D(x, z, chunkSize);
    }

    public static double RoundToDP(float value, int decimalPlaces)
	{
		return System.Math.Round(value, decimalPlaces);
	}

    public static float3 EdgeOverlap(float3 localPosition, int cubeSize)
    {
        float3 floor = Float3Floor(localPosition);
        return new float3(
					floor.x == cubeSize ? 1 : floor.x < 0 ? -1 : 0,
					0,
					floor.z == cubeSize ? 1 : floor.z < 0 ? -1 : 0
					); 
    }

    public static float3 RotateAroundCenter(Quaternion rotation, Vector3 position, Vector3 centre)
    {
        return rotation * (position - centre) + centre;
    }

    public static bool Float3sMatch(float3 a, float3 b)
    {
        if(a.x == b.x && a.y == b.y && a.z ==b.z) return true;
        else return false;
    }

    public static float3 Float3Lerp(float3 a, float3 b, float interpolator)
    {
        float x = math.lerp(a.x, b.x, interpolator);
        float y = math.lerp(a.y, b.y, interpolator);
        float z = math.lerp(a.z, b.z, interpolator);
        return new float3(x, y, z);
    }

    public static int BlockIndex(float3 voxelWorldPosition, MapSquare mapSquare, int cubeSize)
    {
        float3 voxel = voxelWorldPosition - mapSquare.position;
        return Util.Flatten(voxel.x, voxel.y - mapSquare.bottomBlockBuffer, voxel.z, cubeSize);
    }
}
