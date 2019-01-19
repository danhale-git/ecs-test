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

        this.levelFrequency = 0.01f;
        cliffDepth = 0.05f;
        cliffBleed = 0.025f;
    }

    Topology GetHeight(float noise)
    {
        int height = 0;
        TerrainTypes type = 0;

        float increment = 1.0f / levelCount;

        float depth = cliffDepth;
        float bleed = cliffBleed;

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

        return new Topology{
            height = height,
            type = type
        };
    }

    Topology GetCellHeight(CellData cell)
    {
        int height = terrainHeight;
        TerrainTypes type = 0;

        float value = cell.currentCellValue;
        float adjacentValue = cell.adjacentCellValue;

        float increment = 1.0f / levelCount;

        float cellHeight = 0;
        float adjacentHeight = 0;

        //  Get height of cells
        for(int l = 1; l < levelCount; l++)
        {
            float nextStart = (increment * (l+1));
            float prevEnd = (increment * (l-1));

            //  Adjacent cell height
            if((adjacentValue >= prevEnd || l == 0) && (adjacentValue <= nextStart || l == levelCount-1))
            {
                adjacentHeight = levelHeight * l;
            }
            //  This cell height
            if((value >= prevEnd || l == 0) && (value <= nextStart || l == levelCount-1))
            {
                cellHeight = levelHeight * l;
            }
        }
        
        float depth = cliffDepth *2;

        //  Close to the edge between two cells of different heights = cliff
        if(cell.distance2Edge < depth*2 && cellHeight != adjacentHeight)
        {
            type = TerrainTypes.CLIFF;            
        
            //  Closer to the edge between cells, interpolate
            //  between cell heigts for smooth transition
            if(cell.distance2Edge < depth) 
            {
                float halfway = (cellHeight + adjacentHeight) / 2;
                float interpolator = Mathf.InverseLerp(0, depth, cell.distance2Edge);

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

    public MapSquare Generate(float3 position, DynamicBuffer<Topology> heightMap)
    {
        NativeArray<float> noiseMap = noiseGenerator.Simplex(position, levelFrequency);
        NativeArray<CellData> cellMap = noiseGenerator.CellularDistanceToEdge(position, levelFrequency);        

		int highestBlock = 0;
		int lowestBlock = terrainHeight + (levelCount*levelHeight);

        for(int i = 0; i < noiseMap.Length; i++)
        {
            Topology heightComponent = GetCellHeight(cellMap[i]);
            //Topology heightComponent = new Topology {type = 0, height = 20};     
            //Topology heightComponent = GetHeight(noiseMap[i]);
		    
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
            position = new float3(position.x, 0, position.z),
			topBlock    = highestBlock,
			bottomBlock	= lowestBlock
			};
    }


    
}
