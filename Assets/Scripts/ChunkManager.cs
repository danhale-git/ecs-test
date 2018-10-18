using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

using Unity.Entities;
using Unity.Transforms;
using Unity.Rendering;
using Unity.Mathematics;

public class ChunkManager
{
	public static int chunkSize = 8;
    public static int chunkSizeB = 10;

	static EntityManager entityManager;
    static MeshInstanceRenderer renderer;
    static EntityArchetype archetype;

    static FastNoiseJobSystem terrain;
    static GenerateMapSquareJobSystem blockGenerator;
    static CheckBlockExposureJobSystem checkBlockExposure;

    
    public ChunkManager()
    {
        Debug.Log("chunk manager initialised");

		//	Create noise job system
        terrain = new FastNoiseJobSystem();
        blockGenerator = new GenerateMapSquareJobSystem();
        checkBlockExposure = new CheckBlockExposureJobSystem();

        //  Get entity manager
        entityManager = World.Active.GetOrCreateManager<EntityManager>();


        //  New archetype with mesh and position
        archetype = entityManager.CreateArchetype(
            ComponentType.Create<Position>(),
            ComponentType.Create<MeshInstanceRendererComponent>());
    }
	
	public void GenerateChunk()
	{
        Vector3 position = new float3(0, 0, 0);

		//	Entity
		entityManager = World.Active.GetOrCreateManager<EntityManager>();
        Entity meshObject = entityManager.CreateEntity(archetype);
        entityManager.SetComponentData(meshObject, new Position {Value = position});

        //  Generate blocks
        int[] blocks = GenerateBlocks(position);
        int[] exposure = checkBlockExposure.GetExposure(blocks);

		//	Mesh
		List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        
		//	Get mesh
        for(int i = 0; i < math.pow(chunkSize, 3); i++)
        {
            //  Local pos
            float3 pos = Util.Unflatten(i, chunkSize, chunkSize, chunkSize);

            if(blocks[i] != 1) continue;

            int exposureIndex = i * 6;

			//	Get cube mesh
            for(int f = 0; f < 6; f++)
            {
                //if(exposure[exposureIndex+f] == 0) continue;

                triangles.AddRange(MeshManager.Cube.Triangles(vertices.Count));
                vertices.AddRange(MeshManager.Cube.Vertices(f, pos));
            }
        }

		//	Apply mesh
        Material material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/TestMaterial.mat");
        entityManager.AddSharedComponentData(meshObject, MakeMesh(vertices, triangles, material));

		//	Debug wire
        Vector3 center = position + (new Vector3(0.5f, 0.5f, 0.5f) * (chunkSize - 1));
        CustomDebugTools.WireCube(center, chunkSize, Color.green);
	}

    static int[] GenerateBlocks(Vector3 position)
    {
        //	Noise
        int heightMapSize = chunkSizeB;
        float[] noise = terrain.GetSimplexMatrix(position, heightMapSize, 1234, 0.1f);

        //  Noise to height map
        int[] heightMap = new int[noise.Length];
        for(int i = 0; i < noise.Length; i++)
            heightMap[i] = (int)(noise[i] * chunkSize);

        //  Generate blocks
        return blockGenerator.GetBlocks(heightMap);
    }

	//	Create mesh
	static MeshInstanceRenderer MakeMesh(List<Vector3> vertices, List<int> triangles, Material material)
    {
        Mesh mesh = new Mesh();

        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        UnityEditor.MeshUtility.Optimize(mesh);

        renderer = new MeshInstanceRenderer();
        renderer.mesh = mesh;
        renderer.material = material;

        return renderer;
    }
}
