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
    [ReadOnly] public int cubeSize;
    [ReadOnly] public int seed;
    [ReadOnly] public float frequency;
    [ReadOnly] public JobUtil util;
    [ReadOnly] public SimplexNoiseGenerator noise;

    //  Fill flattened 2D array with noise matrix
    public void Execute(int i)
    {
        float3 position = util.Unflatten2D(i, cubeSize) + offset;

        noiseMap[i] = noise.GetSimplex(position.x, position.z, seed, frequency);
    }
}

struct SimplexNoiseGenerator
{
    NativeArray<float2> GRAD_2D;

    int X_PRIME;
    int Y_PRIME;

    public SimplexNoiseGenerator(byte param)
    {
        GRAD_2D = new NativeArray<float2>(8, Allocator.TempJob);
        GRAD_2D[0] = new float2(-1,-1); 
        GRAD_2D[1] = new float2( 1,-1); 
        GRAD_2D[2] = new float2(-1, 1); 
        GRAD_2D[3] = new float2( 1, 1);
        GRAD_2D[4] = new float2( 0,-1); 
        GRAD_2D[5] = new float2(-1, 0); 
        GRAD_2D[6] = new float2( 0, 1); 
        GRAD_2D[7] = new float2( 1, 0);

        X_PRIME = 1619;
        Y_PRIME = 31337;
    }
    public void Dispose()
    {
        GRAD_2D.Dispose();
    }  

    int FastFloor(float f) { return (f >= 0 ? (int)f : (int)f - 1); }

    float GradCoord2D(int seed, int x, int y, float xd, float yd)
    {
        int hash = seed;
        hash ^= X_PRIME * x;
        hash ^= Y_PRIME * y;

        hash = hash * hash * hash * 60493;
        hash = (hash >> 13) ^ hash;

        float2 g = GRAD_2D[hash & 7];

        return xd * g.x + yd * g.y;
    }
        
    public float GetSimplex(float x, float y, int m_seed, float m_frequency)
    {
        return SingleSimplex(m_seed, x * m_frequency, y * m_frequency);
    }

    const float F2 = (float)(1.0 / 2.0);
    const float G2 = (float)(1.0 / 4.0);

    float SingleSimplex(int seed, float x, float y)
    {
        float t = (x + y) * F2;
        int i = FastFloor(x + t);
        int j = FastFloor(y + t);

        t = (i + j) * G2;
        float X0 = i - t;
        float Y0 = j - t;

        float x0 = x - X0;
        float y0 = y - Y0;

        int i1, j1;
        if (x0 > y0)
        {
            i1 = 1; j1 = 0;
        }
        else
        {
            i1 = 0; j1 = 1;
        }

        float x1 = x0 - i1 + G2;
        float y1 = y0 - j1 + G2;
        float x2 = x0 - 1 + F2;
        float y2 = y0 - 1 + F2;

        float n0, n1, n2;

        t = (float)0.5 - x0 * x0 - y0 * y0;
        if (t < 0) n0 = 0;
        else
        {
            t *= t;
            n0 = t * t * GradCoord2D(seed, i, j, x0, y0);
        }

        t = (float)0.5 - x1 * x1 - y1 * y1;
        if (t < 0) n1 = 0;
        else
        {
            t *= t;
            n1 = t * t * GradCoord2D(seed, i + i1, j + j1, x1, y1);
        }

        t = (float)0.5 - x2 * x2 - y2 * y2;
        if (t < 0) n2 = 0;
        else
        {
            t *= t;
            n2 = t * t * GradCoord2D(seed, i + 1, j + 1, x2, y2);
        }

        return To01(50 * (n0 + n1 + n2));
    }

    float To01(float value)
	{
		return (value * 0.5f) + 0.5f;
	}
}