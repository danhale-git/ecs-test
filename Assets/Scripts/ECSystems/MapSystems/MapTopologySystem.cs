﻿using System;
using UnityEngine;
using Unity.Jobs;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Transforms;
using MyComponents;

//  Generate 2D terrain data from coherent noise
[UpdateInGroup(typeof(UpdateGroups.NewMapSquareUpdateGroup))]
public class MapTopologySystem : ComponentSystem
{
    EntityManager entityManager;

    int squareWidth;

    ComponentGroup terrainGroup;

    CliffTerrainGenerator cliffTerrain;

    protected override void OnCreateManager()
    {
        entityManager   = World.Active.GetOrCreateManager<EntityManager>();
        
        squareWidth     = TerrainSettings.mapSquareWidth;

        EntityArchetypeQuery terrainQuery = new EntityArchetypeQuery{
            All     = new ComponentType[] { typeof(MapSquare), typeof(Tags.GenerateTerrain) }
        };
        terrainGroup = GetComponentGroup(terrainQuery);

        cliffTerrain = new CliffTerrainGenerator(5, 10);
    }

    protected override void OnUpdate()
    {
        EntityCommandBuffer         commandBuffer   = new EntityCommandBuffer(Allocator.Temp);
        NativeArray<ArchetypeChunk> chunks          = terrainGroup.CreateArchetypeChunkArray(Allocator.TempJob);

        ArchetypeChunkEntityType                entityType      = GetArchetypeChunkEntityType();
        ArchetypeChunkComponentType<Position>   positionType    = GetArchetypeChunkComponentType<Position>(true);

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
			    heightBuffer.ResizeUninitialized((int)math.pow(squareWidth, 2));

			    //	Fill buffer with heightmap data and update map square highest/lowest block
			    MapSquare mapSquareComponent = cliffTerrain.GenerateTopology(position, heightBuffer);

                //  If map square has been loaded it will already have the correct values
                if(!entityManager.HasComponent<LoadedChange>(entity))
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