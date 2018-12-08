using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Burst;

class FastNoiseJobSystem
{
    public float[] GetSimplexMatrix(int batchSize, float3 chunkPosition, int chunkSize, int seed, float frequency)
    {
        int arrayLength = (int)math.pow(chunkSize, 2);

        //  Native and normal array
        var heightMap = new NativeArray<float>(arrayLength, Allocator.TempJob);
		float[] heightMapArray = new float[arrayLength];

        var job = new FastNoiseJob()
        {
            heightMap = heightMap,
			offset = chunkPosition,
			chunkSize = chunkSize,
            seed = seed,
            frequency = frequency,
			util = new JobUtil(),
            noise = new SimplexNoiseGenerator(0)
        };

        //  Fill native array
        JobHandle jobHandle = job.Schedule(arrayLength, batchSize);
        jobHandle.Complete();

        //  Copy to normal array and return
		heightMap.CopyTo(heightMapArray);
        heightMap.Dispose();
        job.noise.Dispose();

		return heightMapArray;
    }
}