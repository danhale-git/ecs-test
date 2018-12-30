using System;
using UnityEngine;
using Unity.Jobs;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Transforms;
using MyComponents;

[UpdateAfter(typeof(MapSquareSystem))]
public class TerrainSystem : ComponentSystem
{
    EntityManager entityManager;

    int cubeSize;
	int terrainHeight;
	int terrainStretch;

    EntityArchetypeQuery generateTerrainQuery;

    ArchetypeChunkEntityType                entityType;
    ArchetypeChunkComponentType<Position>   positionType;

    protected override void OnCreateManager()
    {
        entityManager   = World.Active.GetOrCreateManager<EntityManager>();
        cubeSize        = TerrainSettings.cubeSize;
        terrainHeight 	= TerrainSettings.terrainHeight;
		terrainStretch 	= TerrainSettings.terrainStretch;

        generateTerrainQuery = new EntityArchetypeQuery
        {
            Any     = Array.Empty<ComponentType>(),
            None    = Array.Empty<ComponentType>(),
            All     = new ComponentType[] { typeof(MapSquare), typeof(Tags.GenerateTerrain) }
        };
    }

    protected override void OnUpdate()
    {
        entityType = GetArchetypeChunkEntityType();
        positionType = GetArchetypeChunkComponentType<Position>();

        NativeArray<ArchetypeChunk> chunks = entityManager.CreateArchetypeChunkArray(
            generateTerrainQuery,
            Allocator.TempJob
            );

        if(chunks.Length == 0) chunks.Dispose();
        else GenerateTerrain(chunks);
    }

    void GenerateTerrain(NativeArray<ArchetypeChunk> chunks)
    {
        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);

        for(int c = 0; c < chunks.Length; c++)
        {
            ArchetypeChunk chunk = chunks[c];

            NativeArray<Entity>     entities    = chunk.GetNativeArray(entityType);
            NativeArray<Position>   positions   = chunk.GetNativeArray(positionType);
            
            for(int e = 0; e < entities.Length; e++)
            {
                Entity entity   = entities[e];
                float3 position = positions[e].Value;

                //	Resize to Dynamic Buffer
                DynamicBuffer<MyComponents.Terrain> heightBuffer = entityManager.GetBuffer<MyComponents.Terrain>(entity);
			    heightBuffer.ResizeUninitialized((int)math.pow(cubeSize, 2));

			    //	Fill buffer with height map data
			    MapSquare mapSquareComponent = GetHeightMap(position, heightBuffer);
			    entityManager.SetComponentData<MapSquare>(entity, mapSquareComponent);

                //  Create cubes next
                commandBuffer.RemoveComponent<Tags.GenerateTerrain>(entity);
                commandBuffer.AddComponent(entity, new Tags.CreateCubes());
            }
        }

    commandBuffer.Playback(entityManager);
    commandBuffer.Dispose();

    chunks.Dispose();
    }

    public MapSquare GetHeightMap(float3 position, DynamicBuffer<MyComponents.Terrain> heightMap)
    {
		//	Flattened 2D array simplex data matrix
        NativeArray<float> noiseMap = new NativeArray<float>((int)math.pow(cubeSize, 2), Allocator.TempJob);

        SimplexNoiseJob simplexJob = new SimplexNoiseJob(){
            noiseMap 	= noiseMap,						//	Flattened 2D array of noise
			offset 		= position,						//	World position of this map square's local 0,0
			cubeSize	= cubeSize,						//	Length of one side of a square/cube	
            seed 		= TerrainSettings.seed,			//	Perlin noise seed
            frequency 	= TerrainSettings.frequency,	//	Perlin noise frequency
			util 		= new JobUtil(),				//	Utilities
            noise 		= new SimplexNoiseGenerator(0)	//	FastNoise.GetSimplex adapted for Jobs
            };

        simplexJob.Schedule(noiseMap.Length, 16).Complete();

        //	Flattened 2D array call data matrix
        NativeArray<CellData> cellMap = new NativeArray<CellData>((int)math.pow(cubeSize, 2), Allocator.TempJob);

        WorleyNoiseJob worleyJob = new WorleyNoiseJob(){
            cellMap 	= cellMap,						//	Flattened 2D array of noise
			offset 		= position,						//	World position of this map square's local 0,0
			cubeSize	= cubeSize,						//	Length of one side of a square/cube	
            seed 		= TerrainSettings.seed,			//	Perlin noise seed
            frequency 	= TerrainSettings.frequency,	//	Perlin noise frequency
            perterbAmp  = 5f,
			util 		= new JobUtil(),				//	Utilities
            noise 		= new WorleyNoiseGenerator(0)	//	FastNoise.GetSimplex adapted for Jobs
            };

        worleyJob.Schedule(noiseMap.Length, 16).Complete();

		//	Convert noise (0-1) into heights (0-maxHeight)
		int highestBlock = 0;
		int lowestBlock = terrainHeight + terrainStretch;

        float cliffStart = 0.45f;
        float cliffEnd = 0.5f;
		for(int i = 0; i < noiseMap.Length; i++)
		{
            int height = 0;
            TerrainTypes type = 0;
			//height = (int)((noiseMap[i] * terrainStretch) + terrainHeight);
			//height = (int)((cellMap[i].distance2Edge * 10) + terrainHeight);

            if(noiseMap[i] > cliffStart)
            {
                if(noiseMap[i] < cliffEnd)
                {
                    float interp = Mathf.InverseLerp(cliffStart, cliffEnd, noiseMap[i]);
                    height = (int)math.lerp(0, 10*interp, interp);
                    type = TerrainTypes.CLIFF;
                }
                else
                {
                    height = 10;
                    type = TerrainTypes.GRASS;
                }
            }
            else
            {
                //height = 0;    
                type = TerrainTypes.GRASS;
            }

            height += terrainHeight;

		    heightMap[i] = new MyComponents.Terrain{
                height = height,
                type = type
            };
				
			if(height > highestBlock)
				highestBlock = height;
			if(height < lowestBlock)
				lowestBlock = height;
        }

		//	Dispose of NativeArrays in noise struct
        simplexJob.noise.Dispose();
        worleyJob.noise.Dispose();
		noiseMap.Dispose();
        cellMap.Dispose();

		return new MapSquare{
			highestVisibleBlock = highestBlock,
			lowestVisibleBlock 	= lowestBlock
			};
    }
}