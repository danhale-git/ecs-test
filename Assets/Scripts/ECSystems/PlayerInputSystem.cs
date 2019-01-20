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
        MovePlayer();

        VoxelRayHit hit;

        bool targetingBlock = SelectBlock(out hit);

        if(Input.GetButtonDown("Fire1") && targetingBlock)
            ChangeBlock(0, hit);
    }

    void MovePlayer()
    {
        float3 playerPosition = entityManager.GetComponentData<Position>(playerEntity).Value;
        PhysicsEntity physicsComponent = entityManager.GetComponentData<PhysicsEntity>(playerEntity);
        Stats stats = entityManager.GetComponentData<Stats>(playerEntity);

        //  Camera forward ignoring x axis tilt
        float3 forward = math.normalize(playerPosition - new float3(camera.transform.position.x, playerPosition.y, camera.transform.position.z));
        float3 right =  camera.transform.right;

         //  Move relative to camera angle
        float3 x = UnityEngine.Input.GetAxis("Horizontal")  * (float3)right;
        float3 z = UnityEngine.Input.GetAxis("Vertical")    * (float3)forward;

        //  Update movement component
        float3 move = (x + z) * stats.speed;
        physicsComponent.positionChangePerSecond = new float3(move.x, 0, move.z);
        entityManager.SetComponentData<PhysicsEntity>(playerEntity, physicsComponent);
    }

    void ChangeBlock(int type, VoxelRayHit hit)
    {
        Block block = entityManager.GetBuffer<Block>(hit.blockOwner)[hit.blockIndex];
        block.type = 0;

        GetOrCreatePendingChangeBuffer(hit.blockOwner).Add(new PendingBlockChange { block = block });
        entityManager.AddComponent(hit.blockOwner, typeof(Tags.BlockChanged));
    }

    struct VoxelRayHit
    {
        readonly public int blockIndex;
        readonly public Entity blockOwner;
        readonly public float3 hitWorldPosition;
        public VoxelRayHit(int blockIndex, Entity blockOwner, float3 hitWorldPosition)
        {
            this.blockIndex         = blockIndex;
            this.blockOwner         = blockOwner;
            this.hitWorldPosition   = hitWorldPosition;
        }
    }

    bool SelectBlock(out VoxelRayHit hit)
    {
        hit = new VoxelRayHit();

        //  Use built in screen to world point ray for origin and direction
        Ray ray = camera.ScreenPointToRay(Input.mousePosition);

        //  Cast ray, get positions of the voxels it hits
        List<float3> traversedVoxelOffsets = VoxelRay(camera.ScreenPointToRay(Input.mousePosition));

        //  Map square at origin
        float3 previousVoxelOwnerPosition = Util.VoxelOwner(ray.origin, cubeSize);
        Entity currentOwner;

        //  Origin entity does not exist
        if(!GetBlockOwner(previousVoxelOwnerPosition, out currentOwner))
            return false;

        //  Check all hit voxels
        for(int i = 0; i < traversedVoxelOffsets.Count; i++)
        {       
            float3 voxelWorldPosition = traversedVoxelOffsets[i];
            float3 nextVoxelOwnerPosition = Util.VoxelOwner(voxelWorldPosition, cubeSize);

            //  Voxel is in a different map square
            if(!Util.Float3sMatch(previousVoxelOwnerPosition, nextVoxelOwnerPosition))
            {
                //  Update current map square
                if(!GetBlockOwner(nextVoxelOwnerPosition, out currentOwner))
                    continue;

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
                hit = new VoxelRayHit(
                    index,
                    currentOwner,
                    voxelWorldPosition
                );
                return true;
            }
        }

        return false;
    }

    bool GetBlockOwner(float3 currentMapSquarePostion, out Entity owner)
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
                owner = entity;
                return true;
            }
        }
        entities.Dispose();
        owner = Entity.Null;
        return false;
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

    //  Return list of voxel positions hit by ray from eye to dir
    List<float3> VoxelRay(Ray ray)
    {
        float3 eye = ray.origin;
        float3 dir = ray.direction;
        int length = 100;

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