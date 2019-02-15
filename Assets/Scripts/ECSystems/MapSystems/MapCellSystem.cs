using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Transforms;
using System.Collections.Generic;
using MyComponents;

[UpdateAfter(typeof(MapManagerSystem))]
public class MapCellSystem : ComponentSystem
{
    EntityManager entityManager;

    int squareWidth;

    ComponentGroup group;

    JobifiedNoise jobifiedNoise;

    protected override void OnCreateManager()
    {
        entityManager = World.Active.GetOrCreateManager<EntityManager>();

        squareWidth = TerrainSettings.mapSquareWidth;

        EntityArchetypeQuery query = new EntityArchetypeQuery{
            All = new ComponentType[] { typeof(Tags.GenerateCells) }
        };
        group = GetComponentGroup(query);

        jobifiedNoise = new JobifiedNoise();
    }

    protected override void OnUpdate()
    {
        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);

        ArchetypeChunkEntityType                entityType      = GetArchetypeChunkEntityType();
        ArchetypeChunkComponentType<Position>   positionType    = GetArchetypeChunkComponentType<Position>(true);

        NativeArray<ArchetypeChunk> chunks = group.CreateArchetypeChunkArray(Allocator.TempJob);

        for(int c = 0; c < chunks.Length; c++)
        {
            NativeArray<Entity>     entities    = chunks[c].GetNativeArray(entityType);
            NativeArray<Position>   positions   = chunks[c].GetNativeArray(positionType);

            for(int e = 0; e < entities.Length; e++)
            {
                Entity entity   = entities[e];
                float3 position = positions[e].Value;

                DynamicBuffer<WorleyNoise> worleyNoiseBuffer = entityManager.GetBuffer<WorleyNoise>(entity);
			    worleyNoiseBuffer.ResizeUninitialized(0);

                NativeArray<WorleyNoise> worleyNoiseMap = jobifiedNoise.CellularDistanceToEdge(position, TerrainSettings.cellFrequency);

                worleyNoiseBuffer.AddRange(worleyNoiseMap);
                worleyNoiseMap.Dispose();

                commandBuffer.RemoveComponent<Tags.GenerateCells>(entity);
            }
        }

        commandBuffer.Playback(entityManager);
        commandBuffer.Dispose();

        chunks.Dispose();
    }
}