using UnityEngine;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Collections;
using MyComponents;
using Unity.Entities;

public class CliffTerrainGenerator
{
    JobifiedNoise noiseGenerator = new JobifiedNoise();

    int terrainHeight = TerrainSettings.terrainHeight;

    int levelCount;
    int levelHeight;
    float levelFrequency;

    float cliffDepth;

    public CliffTerrainGenerator(int levelCount, int levelHeight)
    {
        this.levelCount = levelCount;
        this.levelHeight = levelHeight;

        this.levelFrequency = 0.01f;
        cliffDepth = 0.05f;
    }

    Topology GetCellHeight(CellData cell)
    {
        int height = terrainHeight;
        TerrainTypes type = 0;

        float cellValue = cell.currentCellValue;
        float adjacentValue = cell.adjacentCellValue;

        float increment = 1.0f / levelCount;

        float cellHeight = math.lerp(0, levelCount, cellValue) * levelHeight;
        float adjacentHeight = math.lerp(0, levelCount, adjacentValue) * levelHeight;
        
        //  Close to the edge between two cells of different heights = cliff
        if(cell.distance2Edge < cliffDepth*2 && cellHeight != adjacentHeight)
        {
            type = TerrainTypes.CLIFF;            
        
            //  Closer to the edge between cells, interpolate
            //  between cell heigts for smooth transition
            if(cell.distance2Edge < cliffDepth) 
            {
                float halfway = (cellHeight + adjacentHeight) / 2;
                float interpolator = Mathf.InverseLerp(0, cliffDepth, cell.distance2Edge);

                //  Interpolate towards midpoint using distance from midpoint
                height += (int)math.lerp(halfway, cellHeight, interpolator);
            }
            else
                height += (int)cellHeight;
        }
        //  If not cliff then grass
        else
        {
            type = TerrainTypes.GRASS;
            height += (int)cellHeight;
        }

        return new Topology{
            height = height,
            type = type
        };
    }

    public MapSquare GenerateTopology(float3 position, DynamicBuffer<Topology> heightBuffer)
    {
        NativeArray<float> noiseMap = noiseGenerator.Simplex(position, levelFrequency);
        NativeArray<CellData> cellMap = noiseGenerator.CellularDistanceToEdge(position, levelFrequency);        

		int highestBlock = 0;
		int lowestBlock = terrainHeight + (levelCount*levelHeight);

        for(int i = 0; i < noiseMap.Length; i++)
        {
            Topology heightComponent = GetCellHeight(cellMap[i]);
            //Topology heightComponent = new Topology {type = 0, height = 60};     
            //Topology heightComponent = GetHeight(noiseMap[i]);
		    
            heightBuffer[i] = heightComponent;

            
				
			if(heightComponent.height > highestBlock)
				highestBlock = heightComponent.height;
			if(heightComponent.height < lowestBlock)
				lowestBlock = heightComponent.height;
        }

		//	Dispose of NativeArrays in noise struct
		noiseMap.Dispose();
        cellMap.Dispose();

		return new MapSquare{
            position = new float3(position.x, 0, position.z),
			topBlock    = highestBlock,
			bottomBlock	= lowestBlock
			};
    }


    
}
