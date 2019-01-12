using System;
using UnityEngine;
using Unity.Jobs;
using Unity.Entities;
using Unity.Collections;
using Unity.Transforms;
using Unity.Mathematics;
using MyComponents;

[UpdateAfter(typeof(PlayerInputSystem))]
public class MoveSystem : ComponentSystem
{
    EntityManager entityManager;
    int cubeSize;

    EntityArchetypeQuery query;

    ArchetypeChunkEntityType entityType;
    ArchetypeChunkComponentType<Position> positionType;
    ArchetypeChunkComponentType<Move> moveType;

    protected override void OnCreateManager()
    {
        entityManager = World.Active.GetOrCreateManager<EntityManager>();
        cubeSize = TerrainSettings.cubeSize;

        //  Chunks that need blocks generating
        query = new EntityArchetypeQuery
        {
            Any     = Array.Empty<ComponentType>(),
            None    = Array.Empty<ComponentType>(),
            All     = new ComponentType[] { typeof(Move), typeof(Position) }
        };
    }

    protected override void OnUpdate()
    {
        entityType = GetArchetypeChunkEntityType();
        positionType = GetArchetypeChunkComponentType<Position>();
        moveType = GetArchetypeChunkComponentType<Move>();

        NativeArray<ArchetypeChunk> chunks;
        chunks = entityManager.CreateArchetypeChunkArray(
            query,
            Allocator.TempJob
        );

        if(chunks.Length == 0) chunks.Dispose();
        else MoveEntities(chunks);
    }

    void MoveEntities(NativeArray<ArchetypeChunk> chunks)
    {
        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);

        for(int c = 0; c < chunks.Length; c++)
        {
            ArchetypeChunk chunk = chunks[c];

            NativeArray<Entity> entities = chunk.GetNativeArray(entityType);
            NativeArray<Position> positions = chunk.GetNativeArray(positionType);
            NativeArray<Move> movement = chunk.GetNativeArray(moveType);
            
            for(int e = 0; e < entities.Length; e++)
            {
                float3 newPos = positions[e].Value + (movement[e].positionChangePerSecond * Time.deltaTime);

                float yOffset = newPos.y;

                if(entityManager.Exists(movement[e].currentMapSquare))
                {
                    DynamicBuffer<Topology> heightMap = entityManager.GetBuffer<Topology>(movement[e].currentMapSquare);
                    if(heightMap.Length > 0)
                    {
                        float3 local = Util.LocalPosition(positions[e].Value, cubeSize);
                        yOffset = heightMap[Util.Flatten2D(local.x, local.z, cubeSize)].height;
                    }
                }

                yOffset += movement[e].size.y/2;

                Position newPosition = new Position { Value = new float3(newPos.x, yOffset, newPos.z) };

                commandBuffer.SetComponent<Position>(entities[e], newPosition);
            }
        }

        commandBuffer.Playback(entityManager);
        commandBuffer.Dispose();

        chunks.Dispose();
    }
}