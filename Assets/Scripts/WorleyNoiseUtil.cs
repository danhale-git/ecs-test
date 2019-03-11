using Unity.Collections;
using Unity.Mathematics;
using Unity.Jobs;
using MyComponents;

public struct WorleyNoiseUtil
{
    public NativeArray<WorleyNoise> GetWorleyNoiseMap(float3 position, WorleyNoiseGenerator noise)
    {
        int squareWidth = TerrainSettings.mapSquareWidth;
        
        NativeArray<WorleyNoise> worleyNoiseMap = new NativeArray<WorleyNoise>((int)math.pow(squareWidth, 2), Allocator.TempJob);

        WorleyNoiseJob cellJob = new WorleyNoiseJob(){
            worleyNoiseMap 	= worleyNoiseMap,						//	Flattened 2D array of noise
			offset 		    = position,						        //	World position of this map square's local 0,0
			squareWidth	    = squareWidth,						    //	Length of one side of a square/cube
			util 		    = new JobUtil(),				        //	Utilities
            noise 		    = noise	                                //	FastNoise.GetSimplex adapted for Jobs
        };

        cellJob.Schedule().Complete();

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
