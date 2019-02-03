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
    int squareWidth;

    public static Entity playerEntity;
    Camera camera;
    Entity cursorCube;

    struct VoxelRayHit
    {
        readonly public int blockIndex;
        readonly public Entity blockOwner, faceBlockOwner;
        readonly public Block hitBlock, faceHitBlock;
        readonly public float3 worldPosition;
        public VoxelRayHit(int blockIndex, Entity blockOwner, Block hitBlock, Block faceHitBlock, Entity faceBlockOwner, float3 worldPosition)
        {
            this.blockIndex         = blockIndex;
            this.blockOwner         = blockOwner;
            this.hitBlock           = hitBlock;
            this.faceHitBlock       = faceHitBlock;
            this.faceBlockOwner     = faceBlockOwner;
            this.worldPosition      = worldPosition;
        }
    }

    protected override void OnCreateManager()
    {
        entityManager = World.Active.GetOrCreateManager<EntityManager>();
        squareWidth = TerrainSettings.mapSquareWidth;

        camera = GameObject.FindObjectOfType<Camera>();

        CreateCursorCube();
    }

    void CreateCursorCube()
    {
         EntityArchetype cursorCubeArchetype = entityManager.CreateArchetype(
            ComponentType.Create<Position>(),
            ComponentType.Create<RenderMeshComponent>()
		);

        cursorCube = entityManager.CreateEntity(cursorCubeArchetype);
        CursorCubeMesh(cursorCube);
    }

    Block previousBlock;
    Entity previousBlockOwner;
    protected override void OnUpdate()
    {
        if(Time.fixedTime < 0.5) return;
        
        MovePlayer();

        VoxelRayHit hit;

        bool targetingBlock = VoxelRay(camera.ScreenPointToRay(Input.mousePosition), out hit);

        if(targetingBlock)
        {
            Position newPosition = new Position{ Value = hit.worldPosition };
            entityManager.SetComponentData<Position>(cursorCube, newPosition);
        }

        if(Input.GetButtonDown("Fire1")/* && targetingBlock */)
        {
            if(targetingBlock)
                ChangeBlock(0, hit.hitBlock, hit.blockOwner);
        }
        else if(Input.GetButtonDown("Fire2")/* && targetingBlock */)
        {
            if(targetingBlock)
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
        MapUpdateSystem.GetOrCreatePendingChangeBuffer(owner, entityManager).Add(new PendingChange { block = block });
    }
    void DebugBlock(int debug, Block block, Entity owner)
    {
        block.debug = debug;
        MapUpdateSystem.GetOrCreatePendingChangeBuffer(owner, entityManager).Add(new PendingChange { block = block });
    }

    

    //  Return list of voxel positions hit by ray from eye to dir
    bool VoxelRay(Ray ray, out VoxelRayHit hit)
    {
        hit = new VoxelRayHit();

        //  Map square at origin
        float3 previousVoxelOwnerPosition = Util.VoxelOwner(ray.origin, squareWidth);

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
        int deltaX, deltaY, deltaZ;

        x = (int)math.round(ray.origin.x);
        y = (int)math.round(ray.origin.y);
        z = (int)math.round(ray.origin.z);

        deltaX = (int)math.floor(ray.direction.x * 50);
        deltaY = (int)math.floor(ray.direction.y * 50);
        deltaZ = (int)math.floor(ray.direction.z * 50);

        int stepX, stepY, stepZ, ax, ay, az, bx, by, bz;
        int exy, exz, ezy;

        stepX = (int)math.sign(deltaX);
        stepY = (int)math.sign(deltaY);
        stepZ = (int)math.sign(deltaZ);

        ax = math.abs(deltaX);
        ay = math.abs(deltaY);
        az = math.abs(deltaZ);

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

            float3 nextVoxelOwnerPosition = Util.VoxelOwner(voxel, squareWidth);

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
            int index = Util.BlockIndex(voxel, mapSquare, squareWidth);

            //  Outside map square's generated bounds (no block data)
            if(index >= blocks.Length || index < 0)
                continue;

            //  Found a non-air block
            if(blocks[index].type != 0)
            {
                if(i == 0 || previousVoxel.Equals(float3.zero))
                    throw new Exception("Hit on try "+(i+1)+" with previous hit at "+previousVoxel);

                int previousIndex = Util.BlockIndex(previousVoxel, previousMapSquare, squareWidth);

                hit = new VoxelRayHit(index, entity, blocks[index], blocks[previousIndex], previousEntity, mapSquare.position + blocks[index].localPosition);
                return true;
            }

            previousVoxel = voxel;
            previousEntity = entity;
            previousMapSquare = mapSquare;
        }
        throw new Exception("Ray traversed 1000 voxels without finding anything");
    }

    //	Vertices for normal cube
	void CursorCubeMesh(Entity cubeEntity)
	{	
        CubeVertices baseVerts = new CubeVertices(true);

        Vector3[] vertices = new Vector3[24];

        int[] triangles = new int[36];

        vertices[0]  = baseVerts[5] * 1.2f;
        vertices[1]  = baseVerts[6] * 1.2f;
        vertices[2]  = baseVerts[2] * 1.2f;
        vertices[3]  = baseVerts[1] * 1.2f;
        vertices[4]  = baseVerts[7] * 1.2f;
        vertices[5]  = baseVerts[4] * 1.2f;
        vertices[6]  = baseVerts[0] * 1.2f;
        vertices[7]  = baseVerts[3] * 1.2f;
        vertices[8]  = baseVerts[4] * 1.2f;
        vertices[9]  = baseVerts[5] * 1.2f;
        vertices[10] = baseVerts[1] * 1.2f;
        vertices[11] = baseVerts[0] * 1.2f;
        vertices[12] = baseVerts[6] * 1.2f;
        vertices[13] = baseVerts[7] * 1.2f;
        vertices[14] = baseVerts[3] * 1.2f;
        vertices[15] = baseVerts[2] * 1.2f;
        vertices[16] = baseVerts[7] * 1.2f;
        vertices[17] = baseVerts[6] * 1.2f;
        vertices[18] = baseVerts[5] * 1.2f;
        vertices[19] = baseVerts[4] * 1.2f;
        vertices[20] = baseVerts[0] * 1.2f;
        vertices[21] = baseVerts[1] * 1.2f;
        vertices[22] = baseVerts[2] * 1.2f;
        vertices[23] = baseVerts[3] * 1.2f;

        int index = 0;
        int vertIndex = 0;
        for(int i = 0; i < 6; i++)
        {
            triangles[index+0] = 3 + vertIndex; 
            triangles[index+1] = 1 + vertIndex; 
            triangles[index+2] = 0 + vertIndex; 
            triangles[index+3] = 3 + vertIndex; 
            triangles[index+4] = 2 + vertIndex; 
            triangles[index+5] = 1 + vertIndex;

            index += 6;
            vertIndex += 4;
        }

        Color[] colors = new Color[24];
        
        for(int i = 0; i < 24; i++)
        {
            colors[i] = new Color(1, 1, 1, 0.2f);
        }

        Mesh mesh 		= new Mesh();
		mesh.vertices 	= vertices;
        mesh.colors     = colors; 
		mesh.SetTriangles(triangles, 0);

		mesh.RecalculateNormals();

        RenderMesh renderer = new RenderMesh();
		renderer.mesh = mesh;
		renderer.material = MapMeshSystem.material;

		entityManager.AddSharedComponentData(cubeEntity, renderer);
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