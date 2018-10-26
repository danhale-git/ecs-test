using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEditor;

using Unity.Entities;
using Unity.Transforms;
using Unity.Rendering;
using Unity.Mathematics;
using Unity.Collections;


public class MapManager
{
	//	Settings
	public static int chunkSize = 12;
	public static Material material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/TestMaterial.mat");

	//	Entities
	static EntityManager entityManager;
    static MeshInstanceRenderer renderer;
    static EntityArchetype archetype;

	//	Job Systems
	static FastNoiseJobSystem terrain;
	static GenerateMapSquareJobSystem blockGenerator;
    static CheckBlockExposureJobSystem checkBlockExposure;
    static GenerateMeshJobSystem meshGenerator;

	//	Map data
	static Dictionary<float3, MapSquare> map = new Dictionary<float3, MapSquare>();

	public MapManager()
	{
		//  Get entity manager
        entityManager = World.Active.GetOrCreateManager<EntityManager>();

		//	Job Systems
        terrain = new FastNoiseJobSystem();
		checkBlockExposure = new CheckBlockExposureJobSystem();
        meshGenerator = new GenerateMeshJobSystem();
        blockGenerator = new GenerateMapSquareJobSystem();

		//  New archetype with mesh and position
        archetype = entityManager.CreateArchetype(
            ComponentType.Create<Position>(),
            ComponentType.Create<MeshInstanceRendererComponent>()
            );

		GenerateRadius(Vector3.zero, 2);
	}

	public void GenerateRadius(Vector3 center, int radius)
	{
		PositionsInSpiral(center, radius, CreateStage);
		PositionsInSpiral(center, radius, BlockStage);
		PositionsInSpiral(center, radius, DrawStage);
	}

	void CreateStage(Vector3 position)
	{
		float3 pos = position;
		if(map.ContainsKey(pos)) return;

		MapSquare square = new MapSquare(true);
		map[pos] = square;
	}

	void BlockStage(Vector3 position)
	{
		MapSquare mapSquare = map[(float3)position];
		if(mapSquare.stage != MapSquare.Stages.CREATE) return;

		//	Noise
        float[] noise = terrain.GetSimplexMatrix(position, chunkSize, 5678, 0.05f);

        //  Noise to height map
        int[] heightMap = new int[noise.Length];
        for(int i = 0; i < noise.Length; i++)
            heightMap[i] = (int)(noise[i] * chunkSize);

		mapSquare.blocks = blockGenerator.GetBlocks(heightMap);
		mapSquare.stage = MapSquare.Stages.BLOCKS;
	}

	void DrawStage(Vector3 position)
	{
		MapSquare mapSquare = map[(float3)position];

		//	Entity
        Entity meshObject = entityManager.CreateEntity(archetype);
        entityManager.SetComponentData(meshObject, new Position {Value = position});

		//	Mesh Data
        int faceCount;
        Faces[] exposedFaces = checkBlockExposure.GetExposure(mapSquare.blocks, out faceCount);
		List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();

		//	Apply mesh
        Mesh mesh = meshGenerator.GetMesh(exposedFaces, faceCount);
		renderer = new MeshInstanceRenderer();
        renderer.mesh = mesh;
        renderer.material = material;

        entityManager.AddSharedComponentData(meshObject, renderer);
	}

	delegate void GenerationStage(Vector3 position);
	void PositionsInSpiral(Vector3 center, int radius, GenerationStage delegateOperation)
	{
		Vector3 position = center;
		//	Trim radius to allow buffer of generated chunks
		delegateOperation(position);

		int increment = 1;
		for(int i = 0; i < radius; i++)
		{
			//	right then back
			for(int r = 0; r < increment; r++)
			{
				position += Vector3.right * chunkSize;
				delegateOperation(position);
			}
			for(int b = 0; b < increment; b++)
			{
				position += Vector3.back * chunkSize;
				delegateOperation(position);
			}

			increment++;

			//	left then forward
			for(int l = 0; l < increment; l++)
			{
				position += Vector3.left * chunkSize;
				delegateOperation(position);
			}
			for(int f = 0; f < increment; f++)
			{
				position += Vector3.forward * chunkSize;
				delegateOperation(position);
			}

			increment++;
		}
		//	Square made by spiral is always missing one corner
		for(int r = 0; r < increment - 1; r++)
		{
			position += Vector3.right * chunkSize;
		delegateOperation(position);
		}
	}
}
