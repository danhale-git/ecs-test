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
    float cliffBleed;

    public CliffTerrainGenerator(int levelCount, int levelHeight)
    {
        this.levelCount = levelCount;
        this.levelHeight = levelHeight;

        this.levelFrequency = 0.02f;
        cliffDepth = 0.05f;
        cliffBleed = 0.025f;


        //  DEBUG
        float increment = 1.0f / levelCount;

        float depth = cliffDepth;// * increment;
        float bleed = cliffBleed;// * increment;

        for(int l = 1; l < levelCount; l++)
        {
            int topHeight = levelHeight * l;
            int bottomHeight = topHeight - levelHeight;

            float start = (increment * l) - (depth / 2);
            float end = (increment * l) + (depth / 2);

            float nextStart = (increment * (l+1)) - (depth / 2);
            float prevEnd = (increment * (l-1)) + (depth / 2);

            Debug.Log(l+": "+(start - bleed)+" "+end+bleed);
        }
    }

    MyComponents.Terrain GetHeight(float noise)
    {
        int height = 0;
        TerrainTypes type = 0;

        float increment = 1.0f / levelCount;

        float depth = cliffDepth;// * increment;
        float bleed = cliffBleed;// * increment;

        for(int l = 1; l < levelCount; l++)
        {
            int topHeight = levelHeight * l;
            int bottomHeight = topHeight - levelHeight;

            float start = (increment * l) - (depth / 2);
            float end = (increment * l) + (depth / 2);

            float nextStart = (increment * (l+1)) - (depth / 2);
            float prevEnd = (increment * (l-1)) + (depth / 2);

            if(noise > start - bleed && noise < end + bleed)
            {
                type = TerrainTypes.CLIFF;
                if( noise > start && noise < end)
                {
                    float interp = Mathf.InverseLerp(start, end, noise);
                    height = (int)math.lerp(bottomHeight, topHeight, interp);
                }
                else if(noise >= end) height = topHeight;
                else height = bottomHeight;
                break;
            }
            if(noise <= start - bleed && (l == 0 || noise >= prevEnd + bleed))
            {
                type = TerrainTypes.GRASS;
                height = bottomHeight;
                break;
            }
            if(noise >= end + bleed && (l == levelCount-1 || noise <= nextStart))
            {
                type = TerrainTypes.GRASS;                
                height = topHeight;
                break;
            }
        }

        height += terrainHeight;
        height += (int)math.lerp(0, 6, noise);

        return new MyComponents.Terrain{
            height = height,
            type = type
        };
    }

    MyComponents.Terrain GetCellHeight(CellData cell)
    {
        int height = 0;
        TerrainTypes type = 0;

        float edge = Mathf.InverseLerp(0, 4, cell.distance2Edge);
        float value = cell.currentCellValue;
        float adjacentValue = cell.adjacentCellValue;

        float increment = 1.0f / levelCount;

        float depth = cliffDepth;// * increment;
        float bleed = cliffBleed;// * increment;

        int cellHeight = 0;
        int adjacentHeight = 0;

        for(int l = 1; l < levelCount; l++)
        {
            float start = (increment * l) - (depth / 2);
            float end = (increment * l) + (depth / 2);

            float nextStart = (increment * (l+1)) - (depth / 2);
            float prevEnd = (increment * (l-1)) + (depth / 2);

            if((adjacentValue >= prevEnd || l == 0) && (adjacentValue <= nextStart || l == levelCount-1))
            {
                adjacentHeight = levelHeight * l;
            }
            if((value >= prevEnd || l == 0) && (value <= nextStart || l == levelCount-1))
            {
                cellHeight = levelHeight * l;
            }
        }
        
        height += cellHeight + terrainHeight;

        return new MyComponents.Terrain{
            height = height,
            type = type
        };
    }

    public MapSquare Generate(float3 position, DynamicBuffer<MyComponents.Terrain> heightMap)
    {
        NativeArray<float> noiseMap = noiseGenerator.Simplex(position, levelFrequency);
        NativeArray<CellData> cellMap = noiseGenerator.CellularDistanceToEdge(position, levelFrequency);        

		int highestBlock = 0;
		int lowestBlock = terrainHeight + (levelCount*levelHeight);

        for(int i = 0; i < noiseMap.Length; i++)
        {
            MyComponents.Terrain heightComponent = GetCellHeight(cellMap[i]);            
            //MyComponents.Terrain heightComponent = GetHeight(noiseMap[i]);
		    
            heightMap[i] = heightComponent;

            
				
			if(heightComponent.height > highestBlock)
				highestBlock = heightComponent.height;
			if(heightComponent.height < lowestBlock)
				lowestBlock = heightComponent.height;
        }

		//	Dispose of NativeArrays in noise struct
		noiseMap.Dispose();
        cellMap.Dispose();

		return new MapSquare{
			highestVisibleBlock = highestBlock,
			lowestVisibleBlock 	= lowestBlock
			};
    }


    
}
