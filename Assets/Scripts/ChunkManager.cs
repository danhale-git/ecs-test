using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

using Unity.Entities;
using Unity.Transforms;
using Unity.Rendering;
using Unity.Mathematics;
using Unity.Collections;

public class ChunkManager
{
	public static int chunkSize = 12;
    //public static int chunkSizePlusTwo = chunkSize + 2;

	static EntityManager entityManager;
    static MeshInstanceRenderer renderer;
    static EntityArchetype archetype;

    static GenerateMapSquareJobSystem blockGenerator;
    static CheckBlockExposureJobSystem checkBlockExposure;
    static GenerateMeshJobSystem meshGenerator;


    public ChunkManager()
    {
        Debug.Log("chunk manager initialised");

		//	Create job systems
        checkBlockExposure = new CheckBlockExposureJobSystem();
        meshGenerator = new GenerateMeshJobSystem();
        blockGenerator = new GenerateMapSquareJobSystem();

        //  Get entity manager
        entityManager = World.Active.GetOrCreateManager<EntityManager>();


        //  New archetype with mesh and position
        archetype = entityManager.CreateArchetype(
            ComponentType.Create<Position>(),
            ComponentType.Create<MeshInstanceRendererComponent>()
            );
    }
	
	public void GenerateChunk(Vector3 position, int[] heightMap)
	{
		//	Entity
		entityManager = World.Active.GetOrCreateManager<EntityManager>();
        Entity meshObject = entityManager.CreateEntity(archetype);
        entityManager.SetComponentData(meshObject, new Position {Value = position});

        //  Generate blocks
        int faceCount;
        int[] blocks = blockGenerator.GetBlocks(heightMap);
        Faces[] exposedFaces = checkBlockExposure.GetExposure(blocks, out faceCount);

		//	Mesh
		List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();

		//	Apply mesh
        Mesh mesh = meshGenerator.GetMesh2(exposedFaces, faceCount);
        Material material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/TestMaterial.mat");
        entityManager.AddSharedComponentData(meshObject, MakeMesh(mesh, material));

		//	Debug wire
        Vector3 center = position + (new Vector3(0.5f, 0.5f, 0.5f) * (chunkSize - 1));
        CustomDebugTools.WireCube(center, chunkSize, Color.white);
	}

	//	Create mesh
	static MeshInstanceRenderer MakeMesh(Mesh mesh, Material material)
    {
        renderer = new MeshInstanceRenderer();
        renderer.mesh = mesh;
        renderer.material = material;

        return renderer;
    }
}
