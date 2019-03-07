﻿using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Burst;
using MyComponents;
using Unity.Entities;

//[BurstCompile]
struct WorleyNoiseJob : IJobParallelFor
{
    #region Noise
    
    #endregion
    
    public NativeArray<WorleyNoise> worleyNoiseMap;

    [ReadOnly] public float3 offset;
    [ReadOnly] public int squareWidth;
    [ReadOnly] public int seed;
    [ReadOnly] public float frequency;
	[ReadOnly] public float perterbAmp;
	[ReadOnly] public float cellularJitter;
    [ReadOnly] public JobUtil util;
    [ReadOnly] public WorleyNoiseGenerator noise;

    //  Fill flattened 2D array with noise matrix
    public void Execute(int i)
    {
        float3 position = util.Unflatten2D(i, squareWidth) + offset;

        worleyNoiseMap[i] = noise.GetEdgeData(position.x, position.z, seed, frequency, perterbAmp, cellularJitter);
    }
}