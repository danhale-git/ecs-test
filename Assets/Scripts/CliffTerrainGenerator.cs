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
    int terrainStretch = TerrainSettings.terrainStretch;

    int levelCount;
    int levelHeight;

    float cliffDepth;
    float cliffBleed;

    public CliffTerrainGenerator(int levelCount, int levelHeight)
    {
        this.levelCount = levelCount;
        this.levelHeight = levelHeight;
        cliffDepth = 0.1f;
        cliffBleed = 0.025f;

        float increment = 1.0f / levelCount;

        float depth = cliffDepth * increment;
        float bleed = cliffBleed * increment;

         for(int l = 1; l < levelCount; l++)
        {
            int topHeight = levelHeight * l;
            int bottomHeight = topHeight - levelHeight;

            float start = (increment * l) - (depth / 2);
            float end = (increment * l) + (depth / 2);

            float nextStart = (increment * (l+1)) - (depth / 2);
            float prevEnd = (increment * (l-1)) + (depth / 2);
            Debug.Log(l+" - - - - - - - - ");
            Debug.Log("start:" + start);
            Debug.Log("end:" + end);
            Debug.Log("topHeight:" + topHeight);
            Debug.Log("bottomHeight:" + bottomHeight);
            Debug.Log("depth:" + depth);
            Debug.Log("bleed:" + bleed);
            Debug.Log("increment:" + increment);


        }
    }

    MyComponents.Terrain GetHeight(float noise, float increment, float depth, float bleed)
    {
        int height = 0;
        TerrainTypes type = 0;

        /*float depth = cliffDepth / levelCount;
        float bleed = cliffBleed / levelCount;
        float increment = 1 / levelCount;*/

        bool found = false;

        for(int l = 1; l < levelCount; l++)
        {
            int topHeight = levelHeight * l;
            int bottomHeight = topHeight - levelHeight;

            float start = (increment * l) - (depth / 2);
            float end = (increment * l) + (depth / 2);
            //Debug.Log(start+" "+end);

            float nextStart = (increment * (l+1)) - (depth / 2);
            float prevEnd = (increment * (l-1)) + (depth / 2);

            /*if(noise <= start - bleed && (l == 0 || noise >= prevEnd + bleed))
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
            }*/
            if(noise > start - bleed && noise < end + bleed)
            {
                //Debug.Log("cliff");
                type = TerrainTypes.CLIFF;
                if( noise > start && noise < end)
                {
                    float interp = Mathf.InverseLerp(start, end, noise);
                    height = (int)math.lerp(bottomHeight, topHeight, interp);
                }
                else if(noise >= end) height = topHeight;
                else height = bottomHeight;
                found = true;
                break;
            }
        }

        if(!found)
            for(int l = 1; l < levelCount; l++)
            {
                int topHeight = levelHeight * l;
                int bottomHeight = topHeight - levelHeight;

                float start = (increment * l) - (depth / 2);
                float end = (increment * l) + (depth / 2);
                //Debug.Log(start+" "+end);

                float nextStart = (increment * (l+1)) - (depth / 2);
                float prevEnd = (increment * (l-1)) + (depth / 2);

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

        return new MyComponents.Terrain{
            height = height,
            type = type
        };
    }

    public MapSquare Generate(float3 position, DynamicBuffer<MyComponents.Terrain> heightMap)
    {
        JobifiedNoise noisGenerator = new JobifiedNoise();
		//	Flattened 2D array simplex data matrix
        NativeArray<float> noiseMap = noisGenerator.Simplex(position);


		//	Convert noise (0-1) into heights (0-maxHeight)
		int highestBlock = 0;
		int lowestBlock = terrainHeight + terrainStretch;

        /*float cliffStart = 0.4f;
        float cliffEnd = 0.5f;
        float cliffMargin = 0.025f;
        int cliffHeight = 10;
		for(int i = 0; i < noiseMap.Length; i++)
		{
            int height = 0;
            TerrainTypes type = 0;

            float noise = noiseMap[i];

            if(noise > cliffStart && noise < cliffEnd)
            {
                if(noise > cliffStart+cliffMargin && noise < cliffEnd-cliffMargin)
                {
                    float interp = Mathf.InverseLerp(cliffStart+cliffMargin, cliffEnd-cliffMargin, noise);
                    height = (int)math.lerp(0, cliffHeight, interp);
                }
                else if(noise >= cliffEnd-cliffMargin)
                    height = cliffHeight;
                else
                    height = 0;
                
                
                
                
                type = TerrainTypes.CLIFF;
            }
            else if(noise >= cliffEnd)
            {
                height = cliffHeight;
                type = TerrainTypes.GRASS;
            }
            else
            {
                height = 0;
                type = TerrainTypes.GRASS;
            }

            height += terrainHeight;
            if(type == TerrainTypes.GRASS) height++;

            height += (int)math.lerp(0, 5, noise);*/

        for(int i = 0; i < noiseMap.Length; i++)
        {
            MyComponents.Terrain heightComponent = GetHeight(noiseMap[i], 0.5f, 0.1f, 0.025f);
		    heightMap[i] = heightComponent;
				
			if(heightComponent.height > highestBlock)
				highestBlock = heightComponent.height;
			if(heightComponent.height < lowestBlock)
				lowestBlock = heightComponent.height;
        }

		//	Dispose of NativeArrays in noise struct
		noiseMap.Dispose();

		return new MapSquare{
			highestVisibleBlock = highestBlock,
			lowestVisibleBlock 	= lowestBlock
			};
    }


    
}
