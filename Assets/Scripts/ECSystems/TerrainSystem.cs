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
    
    EntityArchetypeQuery query;

    ArchetypeChunkEntityType entityType;
    ArchetypeChunkComponentType<Position> positionType;

    protected override void OnCreateManager()
    {
        entityManager = World.Active.GetOrCreateManager<EntityManager>();
        cubeSize = TerrainSettings.cubeSize;
        terrainHeight 	= TerrainSettings.terrainHeight;
		terrainStretch 	= TerrainSettings.terrainStretch;

        //  Chunks that need blocks generating
        query = new EntityArchetypeQuery
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

        NativeArray<ArchetypeChunk> chunks;
        chunks = entityManager.CreateArchetypeChunkArray(
            query,
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

            NativeArray<Entity> entities = chunk.GetNativeArray(entityType);
            NativeArray<Position> positions = chunk.GetNativeArray(positionType);
            
            for(int e = 0; e < entities.Length; e++)
            {
                Entity entity = entities[e];
                float3 position = positions[e].Value;

                //	Resize to Dynamic Buffer
			    DynamicBuffer<Height> heightBuffer = entityManager.GetBuffer<Height>(entity);
			    heightBuffer.ResizeUninitialized((int)math.pow(cubeSize, 2));

			    //	Fill buffer with height map data
			    MapSquare mapSquareComponent = GetHeightMap(position, heightBuffer);
			    entityManager.SetComponentData<MapSquare>(entity, mapSquareComponent);

                commandBuffer.RemoveComponent<Tags.GenerateTerrain>(entity);
                commandBuffer.AddComponent(entity, new Tags.CreateCubes());
            }
        }
    commandBuffer.Playback(entityManager);
    commandBuffer.Dispose();

    chunks.Dispose();
    }

    public MapSquare GetHeightMap(float3 position, DynamicBuffer<Height> heightMap)
    {
		//	Flattened 2D array noise data matrix
        var noiseMap = new NativeArray<float>((int)math.pow(cubeSize, 2), Allocator.TempJob);

        var job = new FastNoiseJob(){
            noiseMap 	= noiseMap,						//	Noise map matrix to be filled
			offset 		= position,						//	Position of this map square
			cubeSize	= cubeSize,						//	Length of one side of a square/cube	
            seed 		= TerrainSettings.seed,			//	Perlin noise seed
            frequency 	= TerrainSettings.frequency,	//	Perlin noise frequency
			util 		= new JobUtil(),				//	Utilities
            noise 		= new SimplexNoiseGenerator(0)	//	FastNoise.GetSimplex adapted for Jobs
        };

        job.Schedule(noiseMap.Length, 16).Complete();

		//	Convert noise (0-1) into heights (0-maxHeight)
		//	TODO: Jobify this
		int highestBlock = 0;
		int lowestBlock = terrainHeight + terrainStretch;
		for(int i = 0; i < noiseMap.Length; i++)
		{
			int height = (int)((noiseMap[i] * terrainStretch) + terrainHeight);
		    heightMap[i] = new Height { height = height };
				
			if(height > highestBlock)
				highestBlock = height;
			if(height < lowestBlock)
				lowestBlock = height;
		}

		//	Dispose of NativeArrays in noise struct
        job.noise.Dispose();
		noiseMap.Dispose();

		return new MapSquare{
			highestVisibleBlock 	= highestBlock,
			lowestVisibleBlock 	= lowestBlock
			};
    }
}