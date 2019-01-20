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
    int cubeSize;

    public static Entity playerEntity;
    Camera camera;

    protected override void OnCreateManager()
    {
        entityManager = World.Active.GetOrCreateManager<EntityManager>();
        cubeSize = TerrainSettings.cubeSize;

        camera = GameObject.FindObjectOfType<Camera>();
    }

    protected override void OnUpdate()
    {
        if(Time.fixedTime < 0.5) return;
        
        MovePlayer();

        VoxelRayHit hit;

        //bool targetingBlock = VoxelRay(camera.ScreenPointToRay(Input.mousePosition), out hit);

        if(Input.GetButtonDown("Fire1")/* && targetingBlock */)
        {
            if(VoxelRay(camera.ScreenPointToRay(Input.mousePosition), out hit))
                ChangeBlock(0, hit.hitBlock, hit.blockOwner);
        }
        else if(Input.GetButtonDown("Fire2")/* && targetingBlock */)
        {
            if(VoxelRay(camera.ScreenPointToRay(Input.mousePosition), out hit))
                ChangeBlock(1, hit.faceHitBlock, hit.faceBlockOwner);
        }
    }

    void MovePlayer()
    {
        float3          playerPosition      = entityManager.GetComponentData<Position>(playerEntity).Value;
        PhysicsEntity   physicsComponent    = entityManager.GetComponentData<PhysicsEntity>(playerEntity);
        Stats           stats               = entityManager.GetComponentData<Stats>(playerEntity);

        //  Camera forward ignoring x axis tilt
        float3 forward  = math.normalize(playerPosition - new float3(camera.transform.position.x, playerPosition.y, camera.transform.position.z));

         //  Move relative to camera angle
        float3 x = UnityEngine.Input.GetAxis("Horizontal")  * (float3)camera.transform.right;
        float3 z = UnityEngine.Input.GetAxis("Vertical")    * (float3)forward;

        //  Update movement component
        float3 move = (x + z) * stats.speed;
        physicsComponent.positionChangePerSecond = new float3(move.x, 0, move.z);
        entityManager.SetComponentData<PhysicsEntity>(playerEntity, physicsComponent);
    }

    void ChangeBlock(int type, Block block, Entity owner)
    {
        block.type = type;
        GetOrCreatePendingChangeBuffer(owner).Add(new PendingBlockChange { block = block });
        entityManager.AddComponent(owner, typeof(Tags.BlockChanged));
    }

    struct VoxelRayHit
    {
        readonly public int blockIndex;
        readonly public Entity blockOwner, faceBlockOwner;
        readonly public Block hitBlock, faceHitBlock;
        public VoxelRayHit(int blockIndex, Entity blockOwner, Block hitBlock, Block faceHitBlock, Entity faceBlockOwner)
        {
            this.blockIndex         = blockIndex;
            this.blockOwner         = blockOwner;
            this.hitBlock           = hitBlock;
            this.faceHitBlock       = faceHitBlock;
            this.faceBlockOwner     = faceBlockOwner;
        }
    }

    //  Return list of voxel positions hit by ray from eye to dir
    bool VoxelRay(Ray ray, out VoxelRayHit hit)
    {
        hit = new VoxelRayHit();

        //  Map square at origin
        float3 previousVoxelOwnerPosition = Util.VoxelOwner(ray.origin, cubeSize);

        Entity                  entity;
        MapSquare               mapSquare;
        DynamicBuffer<Block>    blocks;

        //  Origin entity does not exist
        if(!GetBlockOwner(previousVoxelOwnerPosition, out entity))
            throw new Exception("Camera is in non-existent map square");

        mapSquare   = entityManager.GetComponentData<MapSquare>(entity);
        blocks      = entityManager.GetBuffer<Block>(entity);       

        //  Round to closest (up or down) for accurate results
        float3 origin = Util.Float3Round(ray.origin);

        int x, y, z;

        x = (int)math.floor(origin.x);
        y = (int)math.floor(origin.y);
        z = (int)math.floor(origin.z);

        int stepX, stepY, stepZ, ax, ay, az, bx, by, bz;
        int exy, exz, ezy;

        stepX = (int)math.sign(ray.direction.x);
        stepY = (int)math.sign(ray.direction.y);
        stepZ = (int)math.sign(ray.direction.z);

        ax = math.abs((int)math.floor(ray.direction.x * 50));
        ay = math.abs((int)math.floor(ray.direction.y * 50));
        az = math.abs((int)math.floor(ray.direction.z * 50));

        bx = 2 * ax;
        by = 2 * ay;
        bz = 2 * az;

        exy = ay - ax;
        exz = az - ax;
        ezy = ay - az;

        float3 previousVoxel = float3.zero;
        Entity previousEntity = entity;
        MapSquare previousMapSquare = mapSquare;

        //  Traverse voxels
        for(int i = 0; i < 1000; i++)
        {
            //  Current voxel
            float3 voxel = new float3(x, y, z);

            //  March ray to next voxel
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

            float3 nextVoxelOwnerPosition = Util.VoxelOwner(voxel, cubeSize);

            //  Voxel is in a different map square
            if(!previousVoxelOwnerPosition.Equals(nextVoxelOwnerPosition))
            {
                //  Update current map square
                if(!GetBlockOwner(nextVoxelOwnerPosition, out entity))
                    continue;

                mapSquare   = entityManager.GetComponentData<MapSquare>(entity);
                blocks      = entityManager.GetBuffer<Block>(entity);

                //  Hit the edge of the drawn map
                if(entityManager.HasComponent<Tags.InnerBuffer>(entity))
                    return false;
            }

            //  Index in Dynamic Buffer
            int index = Util.BlockIndex(voxel, mapSquare, cubeSize);

            //  Outside map square's generated bounds (no block data)
            if(index >= blocks.Length || index < 0)
                continue;

            //  Found a non-air block
            if(blocks[index].type != 0)
            {
                if(i == 0 || previousVoxel.Equals(float3.zero))
                    throw new Exception("Hit on try "+(i+1)+" with previous hit at "+previousVoxel);

                int previousIndex = Util.BlockIndex(previousVoxel, previousMapSquare, cubeSize);

                hit = new VoxelRayHit(index, entity, blocks[index], blocks[previousIndex], previousEntity);
                return true;
            }

            previousVoxel = voxel;
            previousEntity = entity;
            previousMapSquare = mapSquare;
        }
        throw new Exception("Ray traversed 1000 voxels without finding anything");
    }

    DynamicBuffer<PendingBlockChange> GetOrCreatePendingChangeBuffer(Entity entity)
    {
        DynamicBuffer<PendingBlockChange> changes;

        if(!entityManager.HasComponent<PendingBlockChange>(entity))
            changes = entityManager.AddBuffer<PendingBlockChange>(entity);
        else
            changes = entityManager.GetBuffer<PendingBlockChange>(entity);

        return changes;
    }

    bool GetBlockOwner(float3 currentMapSquarePostion, out Entity owner)
    {
        NativeArray<Entity> entities = entityManager.GetAllEntities(Allocator.TempJob);
        
        for(int i = 0; i< entities.Length; i++)
        {
            if(!entityManager.HasComponent<MapSquare>(entities[i]))
                continue;

            float3 othereMapSquarePosition = entityManager.GetComponentData<MapSquare>(entities[i]).position;

            if(othereMapSquarePosition.Equals(currentMapSquarePostion))
            {
                Entity entity = entities[i];
                entities.Dispose();
                owner = entity;
                return true;
            }
        }
        entities.Dispose();
        owner = Entity.Null;
        return false;
    }
}