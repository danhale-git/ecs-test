using System;
using UnityEngine;
using Unity.Jobs;
using Unity.Entities;
using Unity.Collections;
using Unity.Transforms;
using Unity.Mathematics;
using MyComponents;

[UpdateAfter(typeof(PlayerInputSystem))]
public class PhysicsSystem : ComponentSystem
{
    EntityManager entityManager;
    MapSquareSystem managerSystem;
    int squareWidth;

    ComponentGroup moveGroup;

    protected override void OnCreateManager()
    {
        entityManager = World.Active.GetOrCreateManager<EntityManager>();
        managerSystem = World.Active.GetOrCreateManager<MapSquareSystem>();
        squareWidth = TerrainSettings.mapSquareWidth;

        EntityArchetypeQuery moveQuery = new EntityArchetypeQuery
        {
            Any     = Array.Empty<ComponentType>(),
            None    = Array.Empty<ComponentType>(),
            All     = new ComponentType[] { typeof(PhysicsEntity), typeof(Translation) }
        };
        moveGroup = GetComponentGroup(moveQuery);
    }

    protected override void OnUpdate()
    {
        NativeArray<ArchetypeChunk> chunks = moveGroup.CreateArchetypeChunkArray(Allocator.TempJob);

        ArchetypeChunkEntityType entityType      = GetArchetypeChunkEntityType();
        ArchetypeChunkComponentType<Translation> positionType    = GetArchetypeChunkComponentType<Translation>();
        ArchetypeChunkComponentType<PhysicsEntity> physicsType     = GetArchetypeChunkComponentType<PhysicsEntity>();

        for(int c = 0; c < chunks.Length; c++)
        {
            ArchetypeChunk chunk = chunks[c];

            NativeArray<Entity>         entities    = chunk.GetNativeArray(entityType);
            NativeArray<Translation>       positions   = chunk.GetNativeArray(positionType);
            NativeArray<PhysicsEntity>  physics     = chunk.GetNativeArray(physicsType);
            
            for(int e = 0; e < entities.Length; e++)
            {
                float3 currentPosition = positions[e].Value;
                PhysicsEntity physicsComponent = physics[e];

                float3 nextPosition = currentPosition + (physicsComponent.positionChangePerSecond * Time.deltaTime);

                //  Current map square doesn't exist, find current map Square
                if(!entityManager.Exists(physicsComponent.currentMapSquare))
                    physicsComponent.currentMapSquare = managerSystem.mapMatrix.GetItem(nextPosition);

                if(!entityManager.Exists(physicsComponent.currentMapSquare))
                    continue;   

                if(!entityManager.HasComponent<Topology>(physicsComponent.currentMapSquare))
                    continue;             

                //  Get vector describing next position's overlap from this map square
                float3 currentSquarePosition = entityManager.GetComponentData<MapSquare>(physicsComponent.currentMapSquare).position;               
                float3 overlapDirection = Util.EdgeOverlap(nextPosition - currentSquarePosition, squareWidth);

                //  Next position is outside current map square
                if(!overlapDirection.Equals(float3.zero))
                {
                    //  Get next map square from current map square's AdjacentSquares component                        
                    AdjacentSquares adjacentSquares = entityManager.GetComponentData<AdjacentSquares>(physicsComponent.currentMapSquare);
                    physicsComponent.currentMapSquare = adjacentSquares.GetByDirection(overlapDirection);
                }

                //TODO: proper physics system
                //  Get height of current block
                DynamicBuffer<Topology> heightMap = entityManager.GetBuffer<Topology>(physicsComponent.currentMapSquare);
                if(heightMap.Length == 0)
                {
                    chunks.Dispose();
                    return;
                }

                float3 local = Util.LocalVoxel(nextPosition, squareWidth, true);
                float yOffset = heightMap[Util.Flatten2D(local.x, local.z, squareWidth)].height;

                //  Adjust for model size
                yOffset += physics[e].size.y/2;

                positions[e] = new Translation { Value = new float3(nextPosition.x, yOffset, nextPosition.z) };
                physics[e] = physicsComponent;
            }
        }
        chunks.Dispose();
    }
}