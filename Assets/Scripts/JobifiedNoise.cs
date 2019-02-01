using UnityEngine;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Collections;

public class JobifiedNoise
{
    int squareWidth;

    public JobifiedNoise()
    {
        squareWidth = TerrainSettings.mapSquareWidth;
    }

    public NativeArray<float> Simplex(float3 offset, float frequency = 0.01f)
    {
        NativeArray<float> noiseMap = new NativeArray<float>((int)math.pow(squareWidth, 2), Allocator.TempJob);

        SimplexNoiseJob simplexJob = new SimplexNoiseJob(){
            noiseMap 	= noiseMap,						//	Flattened 2D array of noise
			offset 		= offset,						//	World position of this map square's local 0,0
			squareWidth	= squareWidth,						//	Length of one side of a square/cube	
            seed 		= TerrainSettings.seed,			//	Perlin noise seed
            frequency 	= frequency,	                //	Perlin noise frequency
			util 		= new JobUtil(),				//	Utilities
            noise 		= new SimplexNoiseGenerator(0)	//	FastNoise.GetSimplex adapted for Jobs
            };

        simplexJob.Schedule(noiseMap.Length, 16).Complete();
        simplexJob.noise.Dispose();

        return noiseMap;
    }

    public NativeArray<CellData> CellularDistanceToEdge(float3 position, float frequency = 0.01f)
    {
        NativeArray<CellData> cellMap = new NativeArray<CellData>((int)math.pow(squareWidth, 2), Allocator.TempJob);

        WorleyNoiseJob cellJob = new WorleyNoiseJob(){
            cellMap 	= cellMap,						        //	Flattened 2D array of noise
			offset 		= position,						        //	World position of this map square's local 0,0
			squareWidth	= squareWidth,						        //	Length of one side of a square/cube	
            seed 		= TerrainSettings.seed,			        //	Perlin noise seed
            frequency 	= frequency,	        //	Perlin noise frequency
            perterbAmp  = TerrainSettings.cellGradientPeturb,   //  Gradient Peturb amount
			util 		= new JobUtil(),				        //	Utilities
            noise 		= new WorleyNoiseGenerator(0)	        //	FastNoise.GetSimplex adapted for Jobs
            };

        cellJob.Schedule(cellMap.Length, 16).Complete();

        cellJob.noise.Dispose();

        return cellMap;
    }
}
