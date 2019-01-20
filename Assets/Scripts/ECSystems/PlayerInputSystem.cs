using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Jobs;
using Unity.Entities;
using Unity.Collections;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Rendering;
using MyComponents;

[UpdateAfter(typeof(MapMeshSystem))]
public class PlayerInputSystem : ComponentSystem
{
    EntityManager entityManager;
    public static Entity playerEntity;
    int cubeSize;

    Camera camera;

    protected override void OnCreateManager()
    {
        entityManager = World.Active.GetOrCreateManager<EntityManager>();
        cubeSize = TerrainSettings.cubeSize;

        camera = GameObject.FindObjectOfType<Camera>();
    }

    protected override void OnUpdate()
    {
        ApplyInput();
    }

    void ApplyInput()
    {
        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);

        PhysicsEntity physicsComponent = entityManager.GetComponentData<PhysicsEntity>(playerEntity);
        Stats stats = entityManager.GetComponentData<Stats>(playerEntity);

        //  Move relative to camera angle
        //TODO: camera.transform.forward points downwards, slowing z axis movement
        float3 x = UnityEngine.Input.GetAxis("Horizontal")  * (float3)camera.transform.right;
        float3 z = UnityEngine.Input.GetAxis("Vertical")    * (float3)camera.transform.forward;

        float3 move = (x + z) * stats.speed;

        //  Update movement component
        physicsComponent.positionChangePerSecond = new float3(move.x, 0, move.z);
        entityManager.SetComponentData<PhysicsEntity>(playerEntity, physicsComponent);

        if(Input.GetButtonDown("Fire1"))
        {
            int blockIndex;
            Entity blockOwner;
            if(SelectBlock(out blockIndex, out blockOwner))
            {
                RemoveBlock(commandBuffer, blockIndex, blockOwner);

                Block block = entityManager.GetBuffer<Block>(blockOwner)[blockIndex];

                block.type = 0;

                DynamicBuffer<BlockChange> changes;

                if(!entityManager.HasComponent<BlockChange>(blockOwner))
                    changes = entityManager.AddBuffer<BlockChange>(blockOwner);
                else
                    changes = entityManager.GetBuffer<BlockChange>(blockOwner);

                changes.Add(new BlockChange { newBlock = block });

                entityManager.AddComponent(blockOwner, typeof(Tags.BlockChanged));
            }
        }
    }

    void RemoveBlock(EntityCommandBuffer commandBuffer, int blockIndex, Entity blockOwner)
    {
        DynamicBuffer<Block> blocks = entityManager.GetBuffer<Block>(blockOwner);
        Block block = blocks[blockIndex];
        MapSquare ownerSquare = entityManager.GetComponentData<MapSquare>(blockOwner);
        block.type = 0;
        blocks[blockIndex] = block;

        UpdateMesh(commandBuffer, blockOwner);

        if(block.localPosition.y <= ownerSquare.bottomBlock)
        {
            ownerSquare.bottomBlock = (int)block.localPosition.y - 1;
            entityManager.SetComponentData<MapSquare>(blockOwner, ownerSquare);

            UpdateBuffer(commandBuffer, blockOwner);
        }
    }

    void UpdateMesh(EntityCommandBuffer commandBuffer, Entity entity)
    {
        AddMeshTags(entity);

        AdjacentSquares adjacent = entityManager.GetComponentData<AdjacentSquares>(entity);

        for(int i = 0; i < 4; i++)
        {
            AddMeshTags(adjacent[i]);
            AdjacentSquares otherAdjacent = entityManager.GetComponentData<AdjacentSquares>(adjacent[i]);
            for(int e = 0; e < 4; e++)
            {
                AddMeshTags(otherAdjacent[e]);
            }
        }
    }

    //  
    void AddMeshTags(Entity entity)
    {
        if(!entityManager.HasComponent<Tags.Redraw>(entity) && entityManager.HasComponent<RenderMesh>(entity))
            entityManager.AddComponentData<Tags.Redraw>(entity, new Tags.Redraw());
        if(!entityManager.HasComponent<Tags.DrawMesh>(entity))
            entityManager.AddComponentData<Tags.DrawMesh>(entity, new Tags.DrawMesh());
    }

    void UpdateBuffer(EntityCommandBuffer commandBuffer, Entity entity)
    {
        AddBufferTags(entity);

        AdjacentSquares adjacent = entityManager.GetComponentData<AdjacentSquares>(entity);

        for(int i = 0; i < 4; i++)
        {
            AddBufferTags(adjacent[i]);

            AdjacentSquares otherAdjacent = entityManager.GetComponentData<AdjacentSquares>(adjacent[i]);
            for(int e = 0; e < 4; e++)
            {
                AddOutsideBufferTags(otherAdjacent[e]);
            }
        }
    }

    void AddBufferTags(Entity entity)
    {
        entityManager.AddComponentData<Tags.SetDrawBuffer>(entity, new Tags.SetDrawBuffer());

        entityManager.AddComponentData<Tags.SetBlockBuffer>(entity, new Tags.SetBlockBuffer());

        entityManager.AddComponentData<Tags.BufferChanged>(entity, new Tags.BufferChanged());
    }

    void AddOutsideBufferTags(Entity entity)
    {
        if(!entityManager.HasComponent<Tags.SetBlockBuffer>(entity))
            entityManager.AddComponentData<Tags.SetBlockBuffer>(entity, new Tags.SetBlockBuffer());
            
        if(!entityManager.HasComponent<Tags.BufferChanged>(entity))
            entityManager.AddComponentData<Tags.BufferChanged>(entity, new Tags.BufferChanged());
    }

    bool SelectBlock(out int blockIndex, out Entity blockOwner)
    {
        blockOwner = new Entity();
        blockIndex = 0;
        //  Use built in screen to world point ray for origin and direction
        Ray ray = camera.ScreenPointToRay(Input.mousePosition);
        float3 originVoxel = Util.Float3Round(ray.origin);

        //  Cast ray, get positions of the voxels it hits
        List<float3> traversedVoxelOffsets = VoxelRay(float3.zero, ray.direction, 100);

        //  Map square at origin
        float3 previousVoxelOwnerPosition = Util.VoxelOwner(originVoxel, cubeSize);
        Entity currentOwner = QuickGetOwner(previousVoxelOwnerPosition);

        //  Check all hit voxels
        for(int i = 0; i < traversedVoxelOffsets.Count; i++)
        {       
            float3 voxelWorldPosition = originVoxel + traversedVoxelOffsets[i];
            float3 nextVoxelOwnerPosition = Util.VoxelOwner(voxelWorldPosition, cubeSize);

            //  Voxel is in a different map square
            if(!Util.Float3sMatch(previousVoxelOwnerPosition, nextVoxelOwnerPosition))
            {
                //  Update current map square
                currentOwner = QuickGetOwner(nextVoxelOwnerPosition);

                //  Hit the edge of the drawn map
                if(entityManager.HasComponent<Tags.InnerBuffer>(currentOwner))
                    return false;
            }

            MapSquare currentSquare = entityManager.GetComponentData<MapSquare>(currentOwner);

            //  Index in map square block array
            int index = Util.BlockIndex(
                voxelWorldPosition,
                currentSquare,
                cubeSize
            );

            //  Map square block array
            DynamicBuffer<Block> blocks = entityManager.GetBuffer<Block>(currentOwner);

            //  Outside map square's generated bounds (no block data)
            if(index >= blocks.Length || index < 0) continue;
            
            //  Found a non-air block
            if(blocks[index].type != 0)
            {
                blockOwner = currentOwner;
                blockIndex = index;
                return true;
            }
        }

        return false;
    }

    Entity QuickGetOwner(float3 currentMapSquarePostion)
    {
        NativeArray<Entity> entities = entityManager.GetAllEntities(Allocator.TempJob);
        
        for(int i = 0; i< entities.Length; i++)
        {
            if(!entityManager.HasComponent<MapSquare>(entities[i]))
                continue;

            float3 othereMapSquarePosition = entityManager.GetComponentData<MapSquare>(entities[i]).position;

            if(Util.Float3sMatch(othereMapSquarePosition, currentMapSquarePostion))
            {
                Entity entity = entities[i];
                entities.Dispose();
                return entity;
            }
        }
        entities.Dispose();
        throw new Exception("Could not find entity");
    }

    //  Return list of voxel positions hit by ray from eye to dir
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