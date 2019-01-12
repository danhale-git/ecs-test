using System;
using UnityEngine;
using Unity.Jobs;
using Unity.Entities;
using Unity.Collections;
using Unity.Transforms;
using Unity.Mathematics;
using MyComponents;

[UpdateAfter(typeof(MoveSystem))]
public class CameraSystem : ComponentSystem
{
    EntityManager entityManager;
    int cubeSize;

    EntityArchetypeQuery query;

    ArchetypeChunkEntityType entityType;
    ArchetypeChunkComponentType<Position> positionType;

    //DEBUG
    Camera camera;
    float cameraSwivelSpeed = 1;
    float3 currentOffset = new float3(10, 10, 10);

    protected override void OnCreateManager()
    {
        entityManager = World.Active.GetOrCreateManager<EntityManager>();
        cubeSize = TerrainSettings.cubeSize;

        //  Chunks that need blocks generating
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

        NativeArray<ArchetypeChunk> chunks;
        chunks = entityManager.CreateArchetypeChunkArray(
            query,
            Allocator.TempJob
        );

        if(chunks.Length == 0) chunks.Dispose();
        else DoSomething(chunks);
    }

    void DoSomething(NativeArray<ArchetypeChunk> chunks)
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
                float3 playerPosition = positions[e].Value;

                bool Q = Input.GetKey(KeyCode.Q);
                bool E = Input.GetKey(KeyCode.E);
                Quaternion cameraSwivel = Quaternion.identity;

                if( !(Q && E) )
                {
                    if (Q) cameraSwivel = Quaternion.Euler(new float3(0, cameraSwivelSpeed, 0));
                    else if(E) cameraSwivel = Quaternion.Euler(new float3(0, -cameraSwivelSpeed, 0));
                }

                float3 rotated = Util.RotateAroundCenter(cameraSwivel, camera.transform.position, playerPosition);
                float3 swivel = (float3)camera.transform.position - rotated;

                currentOffset += swivel;

                //DEBUG
                camera.transform.position = playerPosition + currentOffset;
                camera.transform.LookAt(playerPosition);
            }
        }

        commandBuffer.Playback(entityManager);
        commandBuffer.Dispose();

        chunks.Dispose();
    }
}