using System;
using UnityEngine;
using Unity.Jobs;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Transforms;
using MyComponents;

//  Generate 2D terrain data from coherent noise
[UpdateAfter(typeof(MapCreateSystem))]
public class MapTopologySystem : ComponentSystem
{
    EntityManager entityManager;

    int cubeSize;
	int terrainHeight;
	int terrainStretch;

    EntityArchetypeQuery generateTerrainQuery;

    ArchetypeChunkEntityType                entityType;
    ArchetypeChunkComponentType<Position>   positionType;

    CliffTerrainGenerator cliffTerrain;

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

        cliffTerrain = new CliffTerrainGenerator(5, 10);
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
                DynamicBuffer<Topology> heightBuffer = entityManager.GetBuffer<Topology>(entity);
			    heightBuffer.ResizeUninitialized((int)math.pow(cubeSize, 2));

			    //	Fill buffer with height map data
			    MapSquare mapSquareComponent = GetHeightMap(position, heightBuffer);
			    entityManager.SetComponentData<MapSquare>(entity, mapSquareComponent);

                //  Set draw buffer next
                commandBuffer.RemoveComponent<Tags.GenerateTerrain>(entity);
                commandBuffer.AddComponent(entity, new Tags.GetAdjacentSquares());
            }
        }

    commandBuffer.Playback(entityManager);
    commandBuffer.Dispose();

    chunks.Dispose();
    }

    public MapSquare GetHeightMap(float3 position, DynamicBuffer<Topology> heightMap)
    {
        return cliffTerrain.Generate(position, heightMap);
    }
}