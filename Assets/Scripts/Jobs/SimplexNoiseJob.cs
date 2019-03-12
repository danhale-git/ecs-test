using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Burst;

[BurstCompile]
struct SimplexNoiseJob : IJobParallelFor
{
    #region Noise
    
    #endregion
    
    public NativeArray<float> noiseMap;

    [ReadOnly] public float3 offset;
    [ReadOnly] public int squareWidth;
    [ReadOnly] public int seed;
    [ReadOnly] public float frequency;
    [ReadOnly] public JobUtil util;
    [ReadOnly] public SimplexNoiseGenerator noise;

    //  Fill flattened 2D array with noise matrix
    public void Execute(int i)
    {
        float3 position = util.Unflatten2D(i, squareWidth) + offset;

        noiseMap[i] = noise.GetSimplex(position.x, position.z, seed, frequency);
    }
}