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
    MapManagerSystem managerSystem;
    int squareWidth;

    public static Entity playerEntity;
    Camera camera;
    Entity cursorCube;
    Entity cursorCubeDebug1;
    Entity cursorCubeDebug2;

    struct VoxelRayHit
    {
        readonly public Entity hitBlockOwner, faceBlockOwner;
        readonly public Block hitBlock, faceBlock;
        readonly public float3 normal;
        readonly public float3 worldPosition;
        public VoxelRayHit(Entity hitBlockOwner, Block hitBlock, Entity faceBlockOwner, Block faceBlock, float3 normal, float3 worldPosition)
        {
            this.hitBlockOwner      = hitBlockOwner;
            this.hitBlock           = hitBlock;
            this.faceBlockOwner     = faceBlockOwner;
            this.faceBlock          = faceBlock;
            this.normal             = normal;
            this.worldPosition      = worldPosition;
        }
    }

    protected override void OnCreateManager()
    {
        entityManager = World.Active.GetOrCreateManager<EntityManager>();
        managerSystem = World.Active.GetOrCreateManager<MapManagerSystem>();

        squareWidth = TerrainSettings.mapSquareWidth;

        camera = GameObject.FindObjectOfType<Camera>();

        cursorCube = CreateCursorCube(0.8f, Color.white);
        cursorCubeDebug1 = CreateCursorCube(0.2f, Color.red);
        cursorCubeDebug1 = CreateCursorCube(0.2f, Color.green);
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
            //float3 world = hit
            Position newPosition = new Position{ Value = hit.worldPosition + (hit.normal / 5) };
            entityManager.SetComponentData<Position>(cursorCube, newPosition);

            Position debugPos1 = new Position{ Value = hit.worldPosition + hit.normal };
            entityManager.SetComponentData<Position>(cursorCubeDebug1, debugPos1);
        }

        if(Input.GetButtonDown("Fire1")/* && targetingBlock */)
        {
            if(targetingBlock)
                ChangeBlock(0, hit.hitBlock, hit.hitBlockOwner);
        }
        else if(Input.GetButtonDown("Fire2")/* && targetingBlock */)
        {
            if(targetingBlock)
                ChangeBlock(1, hit.faceBlock, hit.faceBlockOwner);
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

        Entity currentOwner;
        float3 previousVoxelOwnerPosition = Util.VoxelOwner(ray.origin, squareWidth);

        //  Origin entity does not exist
        if(!managerSystem.mapMatrix.TryGetFromWorldPosition(previousVoxelOwnerPosition, out currentOwner))
            throw new Exception("Camera is in non-existent map square");

        MapSquare               mapSquare = entityManager.GetComponentData<MapSquare>(currentOwner);
        DynamicBuffer<Block>    blocks = entityManager.GetBuffer<Block>(currentOwner);

        Block previousBlock     = new Block();
        Entity previousOwner    = currentOwner;

        float x = ray.origin.x;
        float y = ray.origin.y;
        float z = ray.origin.z;

        float stepX = math.sign(ray.direction.x);
        float stepY = math.sign(ray.direction.y);
        float stepZ = math.sign(ray.direction.z);

        float ax = math.abs(ray.direction.x);
        float ay = math.abs(ray.direction.y);
        float az = math.abs(ray.direction.z);

        float bx = 2 * ax;
        float by = 2 * ay;
        float bz = 2 * az;

        float exy = ay - ax;
        float exz = az - ax;
        float ezy = ay - az;

        //  Traverse voxels
        for(int i = 0; i < 1000; i++)
        {
            //  Current voxel
            float3 voxel = new float3((int)math.round(x), (int)math.round(y), (int)math.round(z));

            //  March ray to next voxel
            if (exy < 0)
            {
                if (exz < 0)
                {
                    x += stepX;
                    exy += by;
                    exz += bz;
                }
                else
                {
                    z += stepZ;
                    exz -= bx;
                    ezy += by;
                }
            }
            else
            {
                if (ezy < 0)
                {
                    z += stepZ;
                    exz -= bx;
                    ezy += by;
                }
                else
                {
                    y += stepY;
                    exy -= bx;
                    ezy -= bz;
                }
            }

            float3 nextVoxelOwnerPosition = Util.VoxelOwner(voxel, squareWidth);

            //  Voxel is in a different map square
            if(!previousVoxelOwnerPosition.Equals(nextVoxelOwnerPosition))
            {
                //  Update current map square
                if(!managerSystem.mapMatrix.TryGetFromWorldPosition(nextVoxelOwnerPosition, out currentOwner))
                    continue;

                mapSquare   = entityManager.GetComponentData<MapSquare>(currentOwner);
                blocks      = entityManager.GetBuffer<Block>(currentOwner);

                //  Hit the edge of the drawn map
                if(entityManager.HasComponent<Tags.InnerBuffer>(currentOwner))
                    return false;
            }

            //  Index in Dynamic Buffer
            int index = Util.BlockIndex(voxel, mapSquare, squareWidth);

            //  Outside map square's generated bounds (no block data)
            if(index >= blocks.Length || index < 0)
                continue;

            Block block = blocks[index];

            //  Found a non-air block
            if(blocks[index].type != 0)
            {
                float3 hitWorld = block.localPosition + mapSquare.position;
                float3 faceWorld = previousBlock.localPosition + entityManager.GetComponentData<MapSquare>(previousOwner).position;

                hit = new VoxelRayHit(
                    currentOwner,
                    block,
                    previousOwner,
                    previousBlock,
                    faceWorld - hitWorld,
                    hitWorld
                );

                return true;
            }
            
            previousOwner = currentOwner;
            previousBlock = block;
            previousVoxelOwnerPosition = nextVoxelOwnerPosition;
        }
        throw new Exception("Ray traversed 1000 voxels without finding anything");
    }

    Entity CreateCursorCube(float scale, Color color)
    {
         EntityArchetype cursorCubeArchetype = entityManager.CreateArchetype(
            ComponentType.Create<Position>(),
            ComponentType.Create<RenderMeshComponent>()
		);

        Entity cube = entityManager.CreateEntity(cursorCubeArchetype);
        CursorCubeMesh(cube, scale, color);
        return cube;
    }

    //	Vertices for normal cube
	void CursorCubeMesh(Entity cubeEntity, float scale, Color color)
	{	
        CubeVertices baseVerts = new CubeVertices(true);

        Vector3[] vertices = new Vector3[24];

        int[] triangles = new int[36];

        vertices[0]  = baseVerts[5] * scale;
        vertices[1]  = baseVerts[6] * scale;
        vertices[2]  = baseVerts[2] * scale;
        vertices[3]  = baseVerts[1] * scale;
        vertices[4]  = baseVerts[7] * scale;
        vertices[5]  = baseVerts[4] * scale;
        vertices[6]  = baseVerts[0] * scale;
        vertices[7]  = baseVerts[3] * scale;
        vertices[8]  = baseVerts[4] * scale;
        vertices[9]  = baseVerts[5] * scale;
        vertices[10] = baseVerts[1] * scale;
        vertices[11] = baseVerts[0] * scale;
        vertices[12] = baseVerts[6] * scale;
        vertices[13] = baseVerts[7] * scale;
        vertices[14] = baseVerts[3] * scale;
        vertices[15] = baseVerts[2] * scale;
        vertices[16] = baseVerts[7] * scale;
        vertices[17] = baseVerts[6] * scale;
        vertices[18] = baseVerts[5] * scale;
        vertices[19] = baseVerts[4] * scale;
        vertices[20] = baseVerts[0] * scale;
        vertices[21] = baseVerts[1] * scale;
        vertices[22] = baseVerts[2] * scale;
        vertices[23] = baseVerts[3] * scale;

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
            colors[i] = color;
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
}