using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Jobs;
using Unity.Entities;
using Unity.Collections;
using Unity.Transforms;
using Unity.Mathematics;
using MyComponents;

[UpdateAfter(typeof(MeshSystem))]
public class PlayerInputSystem : ComponentSystem
{
    EntityManager entityManager;
    int cubeSize;

    EntityArchetypeQuery query;

    ArchetypeChunkEntityType                    entityType;
    ArchetypeChunkComponentType<Position>       positionType;
    ArchetypeChunkComponentType<PhysicsEntity>  physicsType;
    ArchetypeChunkComponentType<Stats>          statsType;

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

        camera = GameObject.FindObjectOfType<Camera>();
    }

    protected override void OnUpdate()
    {
        entityType      = GetArchetypeChunkEntityType();
        positionType    = GetArchetypeChunkComponentType<Position>();
        physicsType     = GetArchetypeChunkComponentType<PhysicsEntity>();
        statsType       = GetArchetypeChunkComponentType<Stats>();

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

            NativeArray<Entity> entities        = chunk.GetNativeArray(entityType);
            NativeArray<Position> positions     = chunk.GetNativeArray(positionType);
            NativeArray<PhysicsEntity> physics  = chunk.GetNativeArray(physicsType);
            NativeArray<Stats> stats            = chunk.GetNativeArray(statsType);
            
            for(int e = 0; e < entities.Length; e++)
            {
                Entity entity = entities[e];

                //  Move relative to camera angle
                //TODO: camera.transform.forward points downwards, slowing z axis movement
                float3 x = UnityEngine.Input.GetAxis("Horizontal")  * (float3)camera.transform.right;
                float3 z = UnityEngine.Input.GetAxis("Vertical")    * (float3)camera.transform.forward;

                float3 move = (x + z) * stats[e].speed;

                //  Update movement component
                PhysicsEntity physicsComponent = physics[e];
                physicsComponent.positionChangePerSecond = new float3(move.x, 0, move.z);
                physics[e] = physicsComponent;

                if(Input.GetButtonDown("Fire1"))
                {
                    SelectBlock();
                }
            }
        }
        chunks.Dispose();
    }

    void SelectBlock()
    {
        float3 rayStart = Util.Float3Floor(camera.transform.position);
        Debug.Log("start: "+rayStart);

        float3 direction = camera.ScreenPointToRay(Input.mousePosition).direction;

        List<float3> voxels = VoxelRay(float3.zero, direction, 100);

        Entity currentOwner = QuickGetEntity(rayStart);
        MapSquare currentMapSquare = entityManager.GetComponentData<MapSquare>(currentOwner);
        float3 previousVoxelOwnerPosition = currentMapSquare.position;

        for(int i = 0; i < voxels.Count; i++)
        {       
            float3 nextVoxelOwnerPosition = Util.VoxelOwner(voxels[i], cubeSize);

            if(!Util.Float3sMatch(previousVoxelOwnerPosition, nextVoxelOwnerPosition))
            {
                currentOwner = QuickGetEntity(voxels[i] + rayStart);
                currentMapSquare = entityManager.GetComponentData<MapSquare>(currentOwner);

                //  edge of map
                if(entityManager.HasComponent<Tags.InnerBuffer>(currentOwner))
                {
                    Debug.Log("HIT MAP EDGE");
                    return;
                }
            }

            float3 voxelWorldPosition = rayStart + voxels[i];
            int index = Util.BlockIndex(voxelWorldPosition, currentMapSquare, cubeSize);
            DynamicBuffer<Block> blocks = entityManager.GetBuffer<Block>(currentOwner);
            if(index >= blocks.Length || index < 0) continue;
            
            

            if(blocks[index].type != 0)
            {
                Block block = blocks[index];
                block.type = 0;
                blocks[index] = block;
                //GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                //cube.transform.position = voxels[i] + (float3)camera.transform.position;
                //CustomDebugTools.SetMapSquareHighlight(currentOwner, cubeSize-1, Color.red, 50, 0);
                return;
            }
        }
    }

    Entity QuickGetEntity(float3 voxel)
    {
        NativeArray<Entity> entities = entityManager.GetAllEntities(Allocator.TempJob);
        for(int i = 0; i< entities.Length; i++)
        {
            if(!entityManager.HasComponent<MapSquare>(entities[i]))
                continue;
            float3 position = entityManager.GetComponentData<MapSquare>(entities[i]).position;

            if(Util.Float3sMatch(position, Util.VoxelOwner(voxel, cubeSize)))
            {
                Entity entity = entities[i];
                entities.Dispose();
                return entity;
            }
        }
        entities.Dispose();
        throw new Exception("Could not find entity");
    }

    List<float3> VoxelRay(Vector3 eye, Vector3 dir, int length)
    {
        int x, y, z;
        int deltaX, deltaY, deltaZ;

        x = Mathf.FloorToInt(eye.x);
        y = Mathf.FloorToInt(eye.y);
        z = Mathf.FloorToInt(eye.z);

        deltaX = Mathf.FloorToInt(dir.x * length);
        deltaY = Mathf.FloorToInt(dir.y * length);
        deltaZ = Mathf.FloorToInt(dir.z * length);

        int n, stepX, stepY, stepZ, ax, ay, az, bx, by, bz;
        int exy, exz, ezy;

        stepX = (int)Mathf.Sign(deltaX);
        stepY = (int)Mathf.Sign(deltaY);
        stepZ = (int)Mathf.Sign(deltaZ);

        ax = Mathf.Abs(deltaX);
        ay = Mathf.Abs(deltaY);
        az = Mathf.Abs(deltaZ);

        bx = 2 * ax;
        by = 2 * ay;
        bz = 2 * az;

        exy = ay - ax;
        exz = az - ax;
        ezy = ay - az;

        Gizmos.color = Color.white;

        var start = new Vector3(x, y, z);
        var end = new Vector3(x + deltaX, y + deltaY, z + deltaZ);

        List<float3> voxels = new List<float3>();

        n = ax + ay + az;
        for(int i = 0; i < length; i++)
        {
            voxels.Add(new float3(x, y, z));

            if (exy < 0)
            {
                if (exz < 0)
                {
                    x += stepX;
                    exy += by; exz += bz;
                }
                else
                {
                    z += stepZ;
                    exz -= bx; ezy += by;
                }
            }
            else
            {
                if (ezy < 0)
                {
                    z += stepZ;
                    exz -= bx; ezy += by;
                }
                else
                {
                    y += stepY;
                    exy -= bx; ezy -= bz;
                }
            }
        }
        return voxels;
    }
}