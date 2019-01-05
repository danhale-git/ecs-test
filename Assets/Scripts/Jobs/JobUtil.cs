using Unity.Mathematics;

public struct JobUtil
{
    public int Flatten2(int x, int y, int z, int width, int height)
    {
        return (z * width * height) + (y * width) + x;
    }

    public float3 Unflatten2(int index, int width, int height)
    {
        int z = index / (width * height);
        index -= (z * width * height);
        int y = index / width;
        int x = index % width;
        return new float3 ( x, y, z );
    }

    /*public float3 Unflatten(int index, int xLength, int yLength=0, int zLength=0)
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
    }  */
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
    public int Flatten2D(float x, float z, int size)
    {
        return ((int)z * size) + (int)x;
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

    public int BlockIndex(float3 pos, int cubeSize)
    {
        int cubesUp = (int)math.floor(pos.y / cubeSize);
        int startIndex = (cubesUp * (int)math.pow(cubeSize, 3));

        return startIndex + Flatten(pos.x, pos.y - (cubesUp * cubeSize), pos.z, cubeSize);
    }
}