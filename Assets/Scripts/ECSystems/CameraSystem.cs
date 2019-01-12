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

    public static Entity playerEntity;

    EntityArchetypeQuery query;

    ArchetypeChunkEntityType entityType;
    ArchetypeChunkComponentType<Position> positionType;

    //DEBUG
    Camera camera;
    float cameraSwivelSpeed = 1;
    float3 currentOffset = new float3(0, 15, 10);

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
        else MoveCamera(chunks);
    }

    void MoveCamera(NativeArray<ArchetypeChunk> chunks)
    {
        for(int c = 0; c < chunks.Length; c++)
        {
            ArchetypeChunk chunk = chunks[c];

            NativeArray<Entity> entities = chunk.GetNativeArray(entityType);
            NativeArray<Position> positions = chunk.GetNativeArray(positionType);
            
            for(int e = 0; e < entities.Length; e++)
            {
                Entity entity = entities[e];
                float3 playerPosition = positions[e].Value;
                float3 oldPosition = camera.transform.position;
                Quaternion oldRotation = camera.transform.rotation;

                //  Rotate around player y axis
                bool Q = Input.GetKey(KeyCode.Q);
                bool E = Input.GetKey(KeyCode.E);
                Quaternion cameraSwivel = Quaternion.identity;
                if( !(Q && E) )
                {
                    if (Q) cameraSwivel = Quaternion.Euler(new float3(0, -cameraSwivelSpeed, 0));
                    else if(E) cameraSwivel = Quaternion.Euler(new float3(0, cameraSwivelSpeed, 0));
                }
                float3 rotateOffset = (float3)oldPosition - Util.RotateAroundCenter(cameraSwivel, oldPosition, playerPosition);

                //  Zoom with mouse wheel
                float3 zoomOffset = Input.GetAxis("Mouse ScrollWheel") * -currentOffset;

                //  Apply position changes
                currentOffset += rotateOffset;

                float3 withZoom = currentOffset + zoomOffset;

                //  Clamp zoom - x & z lerped for zoom because y is lerped for everything later
                float magnitude = math.sqrt(math.pow(withZoom.x, 2) + math.pow(withZoom.y, 2) + math.pow(withZoom.z, 2));
                if(magnitude > 10 && magnitude < 40)
                    currentOffset = new float3(
                        math.lerp(currentOffset.x, withZoom.x, 0.1f),
                        withZoom.y,
                        math.lerp(currentOffset.z, withZoom.z, 0.1f)
                    );

                float3 newPosition =  playerPosition + currentOffset;
                Quaternion newRotation = Quaternion.LookRotation(playerPosition - (float3)oldPosition, Vector3.up);

                //  Lerp y for everything - x and z stay tight because horizontal movement depends on camera direction
                float yLerp = math.lerp(oldPosition.y, newPosition.y, 0.1f);

                //  Apply new position and rotation softly
                camera.transform.position = new float3(newPosition.x, yLerp, newPosition.z);
                camera.transform.rotation = Quaternion.Lerp(oldRotation, newRotation, 0.1f);
            }
        }
        chunks.Dispose();
    }
}