using System;
using UnityEngine;
using Unity.Jobs;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Transforms;
using MyComponents;

//  Generate 2D terrain data from coherent noise
[UpdateAfter(typeof(MapUpdateSystem))]
public class MapTopologySystem : ComponentSystem
{
    EntityManager entityManager;
    int cubeSize;

    ComponentGroup terrainGroup;

    CliffTerrainGenerator cliffTerrain;

    protected override void OnCreateManager()
    {
        entityManager   = World.Active.GetOrCreateManager<EntityManager>();
        cubeSize        = TerrainSettings.cubeSize;

        EntityArchetypeQuery terrainQuery = new EntityArchetypeQuery{
            All     = new ComponentType[] { typeof(MapSquare), typeof(Tags.GenerateTerrain) }
        };
        terrainGroup = GetComponentGroup(terrainQuery);

        cliffTerrain = new CliffTerrainGenerator(5, 10);
    }

    protected override void OnUpdate()
    {
        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);

        ArchetypeChunkEntityType                entityType      = GetArchetypeChunkEntityType();
        ArchetypeChunkComponentType<Position>   positionType    = GetArchetypeChunkComponentType<Position>();

        NativeArray<ArchetypeChunk> chunks = terrainGroup.CreateArchetypeChunkArray(Allocator.TempJob);

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

			    //	Fill buffer with heightmap data and update map square highest/lowest block
			    MapSquare mapSquareComponent = cliffTerrain.GenerateTopology(position, heightBuffer);
			    entityManager.SetComponentData<MapSquare>(entity, mapSquareComponent);

                //  Set draw buffer next
                commandBuffer.RemoveComponent<Tags.GenerateTerrain>(entity);
            }
        }

        commandBuffer.Playback(entityManager);
        commandBuffer.Dispose();

        chunks.Dispose();
    }
}