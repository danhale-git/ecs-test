using Unity.Collections;
using Unity.Mathematics;
using Unity.Jobs;
using MyComponents;

public struct WorleyNoiseUtil
{
    public NativeArray<WorleyNoise> GetWorleyNoiseMap(float3 position)
    {
        int squareWidth = TerrainSettings.mapSquareWidth;
        
        NativeArray<WorleyNoise> worleyNoiseMap = new NativeArray<WorleyNoise>((int)math.pow(squareWidth, 2), Allocator.TempJob);

        WorleyNoiseJob cellJob = new WorleyNoiseJob(){
            worleyNoiseMap 	= worleyNoiseMap,						//	Flattened 2D array of noise
			offset 		    = position,						        //	World position of this map square's local 0,0
			squareWidth	    = squareWidth,						    //	Length of one side of a square/cube
            seed 		    = TerrainSettings.seed,			        //	Perlin noise seed
            frequency 	    = TerrainSettings.cellFrequency,	    //	Perlin noise frequency
            perterbAmp      = TerrainSettings.cellEdgeSmoothing,    //  Gradient Peturb amount
            cellularJitter  = TerrainSettings.cellularJitter,       //  Randomness of cell shapes
			util 		    = new JobUtil(),				        //	Utilities
            noise 		    = new WorleyNoiseGenerator(0)	        //	FastNoise.GetSimplex adapted for Jobs
        };

        cellJob.Schedule(worleyNoiseMap.Length, 16).Complete();

        cellJob.noise.Dispose();

        return worleyNoiseMap;
    }
    
    public NativeArray<WorleyCell> UniqueWorleyCellSet(NativeArray<WorleyNoise> worleyNoiseMap)
    {
        NativeList<WorleyNoise> noiseSet = Util.Set<WorleyNoise>(worleyNoiseMap, Allocator.Temp);
        NativeArray<WorleyCell> cellSet = new NativeArray<WorleyCell>(noiseSet.Length, Allocator.TempJob);

        for(int i = 0; i < noiseSet.Length; i++)
        {
            WorleyNoise worleyNoise = noiseSet[i];

            WorleyCell cell = new WorleyCell {
                value = worleyNoise.currentCellValue,
                index = worleyNoise.currentCellIndex,
                position = worleyNoise.currentCellPosition
            };

            cellSet[i] = cell;
        }

        return cellSet;
    }
}
