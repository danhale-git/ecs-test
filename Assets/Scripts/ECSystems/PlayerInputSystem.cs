using System;
using UnityEngine;
using Unity.Jobs;
using Unity.Entities;
using Unity.Collections;
using Unity.Transforms;
using Unity.Mathematics;
using MyComponents;

[UpdateAfter(typeof(MapSquareSystem))]
public class PlayerInputSystem : ComponentSystem
{
    EntityManager entityManager;
    int cubeSize;

    EntityArchetypeQuery query;

    ArchetypeChunkEntityType entityType;
    ArchetypeChunkComponentType<Position> positionType;
    ArchetypeChunkComponentType<Movement> moveType;
    ArchetypeChunkComponentType<Stats> statsType;

    //DEBUG
    Camera camera;

    protected override void OnCreateManager()
    {
        entityManager = World.Active.GetOrCreateManager<EntityManager>();
        cubeSize = TerrainSettings.cubeSize;

        query = new EntityArchetypeQuery
        {
            Any     = Array.Empty<ComponentType>(),
            None    = Array.Empty<ComponentType>(),
            All     = new ComponentType[] { typeof(Tags.PlayerEntity) }
        };

        //DEBUG
        camera = GameObject.FindObjectOfType<Camera>();
    }

    protected override void OnUpdate()
    {
        entityType = GetArchetypeChunkEntityType();
        positionType = GetArchetypeChunkComponentType<Position>();
        moveType = GetArchetypeChunkComponentType<Movement>();
        statsType = GetArchetypeChunkComponentType<Stats>();

        NativeArray<ArchetypeChunk> chunks;
        chunks = entityManager.CreateArchetypeChunkArray(
            query,
            Allocator.TempJob
        );

        if(chunks.Length == 0) chunks.Dispose();
        else ApplyInput(chunks);
    }

    void ApplyInput(NativeArray<ArchetypeChunk> chunks)
    {
        for(int c = 0; c < chunks.Length; c++)
        {
            ArchetypeChunk chunk = chunks[c];

            NativeArray<Entity> entities = chunk.GetNativeArray(entityType);
            NativeArray<Position> positions = chunk.GetNativeArray(positionType);
            NativeArray<Movement> movement = chunk.GetNativeArray(moveType);
            NativeArray<Stats> stats = chunk.GetNativeArray(statsType);
            
            for(int e = 0; e < entities.Length; e++)
            {
                Entity entity = entities[e];


                float3 x = UnityEngine.Input.GetAxis("Horizontal") * (float3)camera.transform.right;
                float3 z = UnityEngine.Input.GetAxis("Vertical") * (float3)camera.transform.forward;

                float3 move = (x + z) * stats[e].speed;

                Movement moveComponent = movement[e];
                
                moveComponent.positionChangePerSecond = new float3(move.x, 0, move.z);

                movement[e] = moveComponent;
            }
        }
        chunks.Dispose();
    }
}