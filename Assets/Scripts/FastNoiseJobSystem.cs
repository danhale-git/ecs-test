using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

class FastNoiseJobSystem
{
    struct SimplexJob : IJobParallelFor
    {
        //  Copied from FastNoise.cs
        #region Noise
        const int X_PRIME = 1619;
	    const int Y_PRIME = 31337;

        static readonly Float2[] GRAD_2D = {
		new Float2(-1,-1), new Float2( 1,-1), new Float2(-1, 1), new Float2( 1, 1),
		new Float2( 0,-1), new Float2(-1, 0), new Float2( 0, 1), new Float2( 1, 0),
	    };

        private struct Float2
        {
            public readonly float x, y;
            public Float2(float x, float y)
            {
                this.x = x;
                this.y = y;
            }
        }

        int FastFloor(float f) { return (f >= 0 ? (int)f : (int)f - 1); }

        float GradCoord2D(int seed, int x, int y, float xd, float yd)
        {
            int hash = seed;
            hash ^= X_PRIME * x;
            hash ^= Y_PRIME * y;

            hash = hash * hash * hash * 60493;
            hash = (hash >> 13) ^ hash;

            Float2 g = GRAD_2D[hash & 7];

            return xd * g.x + yd * g.y;
        }
            
        float GetSimplex(float x, float y, int m_seed, float m_frequency)
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

            return 50 * (n0 + n1 + n2);
        }
        #endregion
        
		public NativeArray<float> heightMap;

        [ReadOnly] public float3 offset;
        [ReadOnly] public int chunkSize;
        [ReadOnly] public int seed;
        [ReadOnly] public float frequency;
        [ReadOnly] public JobUtil util;

        //  Fill flattened 2D array with noise matrix
        public void Execute(int i)
        {
            float3 position = util.Unflatten2D(i, chunkSize) + offset;

            if(position.y == 50) Debug.Log(util.Unflatten2D(i, chunkSize)+" + "+offset+" = "+position);

			heightMap[i] = util.To01(GetSimplex(position.x, position.z, seed, frequency));
        }
    }

    public float[] GetSimplexMatrix(int batchSize, float3 chunkPosition, int chunkSize, int seed, float frequency)
    {
        int arrayLength = (int)math.pow(chunkSize, 2);

        //  Native and normal array
        var heightMap = new NativeArray<float>(arrayLength, Allocator.TempJob);
		float[] heightMapArray = new float[arrayLength];

        var job = new SimplexJob()
        {
            heightMap = heightMap,
			offset = chunkPosition,
			chunkSize = chunkSize,
            seed = seed,
            frequency = frequency,
			util = new JobUtil()
        };

        //  Fill native array
        JobHandle jobHandle = job.Schedule(arrayLength, batchSize);
        jobHandle.Complete();

        //  Copy to normal array and return
		heightMap.CopyTo(heightMapArray);
        heightMap.Dispose();

		return heightMapArray;
    }
}