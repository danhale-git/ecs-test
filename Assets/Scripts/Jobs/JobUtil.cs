using Unity.Mathematics;

public struct JobUtil
{
    public int Flatten(int x, int y, int z, int width)
    {
        return ((z * width) + x) + (y * (width * width));
    }

    public float3 Unflatten(int index, int width)
    {
        int y = (int)math.floor(index / (width * width));
        index -= y * (width * width);
        int z = (int)math.floor(index / width);
        int x = index - (width * z);
        return new float3(x, y, z);
    }

    public int Flatten(float3 xyz, int width)
    {
        return (((int)xyz.z * width) + (int)xyz.x) + ((int)xyz.y * (width * width));
        //return (int)(xyz.z + size * (xyz.y + size * xyz.x));
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
		//int y = index.y;
		int z = index.z;

		if(x == -1) 
			x = chunkSize-1; 
		else if(x == chunkSize) 
			x = 0;

		/*if(y == -1) 
			y = chunkSize-1; 
		else if(y == chunkSize) 
			y = 0; */

		if(z == -1) 
			z = chunkSize-1; 
		else if(z == chunkSize) 
			z = 0;

		return new int3(x, index.y, z);
	}

    public int WrapAndFlatten(int3 position, int chunkSize)
    {
        return Flatten(WrapBlockIndex(position, chunkSize), chunkSize);
    }

    public float3 EdgeOverlap(float3 localPosition, int cubeSize)
    {
        return new float3(
					localPosition.x == cubeSize ? 1 : localPosition.x < 0 ? -1 : 0,
					0,
					localPosition.z == cubeSize ? 1 : localPosition.z < 0 ? -1 : 0
					); 
    }
}