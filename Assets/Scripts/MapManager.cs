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
	public static int chunkSize = 8;
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

		//GenerateRadius(new Vector3(100, 50, 100), 2);
		GenerateRadius(Vector3.zero, 2);
	}

	public void GenerateRadius(Vector3 center, int radius)
	{
		PositionsInSpiral(center, radius+1, CreateStage);
		PositionsInSpiral(center, radius+1, BlockStage);
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
		Debug.Log("BlockStage: "+position);
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
        Faces[] exposedFaces = checkBlockExposure.GetExposure(
			GetAdjacentSquares(position),
			mapSquare.blocks,
			out faceCount);

		List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();

		//	Apply mesh
        Mesh mesh = meshGenerator.GetMesh(exposedFaces, faceCount);
		renderer = new MeshInstanceRenderer();
        renderer.mesh = mesh;
        renderer.material = material;

        entityManager.AddSharedComponentData(meshObject, renderer);
	}

	int[][] GetAdjacentSquares(Vector3 position)
	{
		float3 pos = position;
		int[][] adjacent = new int[6][];
		
		adjacent[0] = map[pos + (new float3( 1,	0, 0) * chunkSize)].blocks;
		adjacent[1] = map[pos + (new float3(-1,	0, 0) * chunkSize)].blocks;

		//adjacent[2] = map[pos + (new float3( 0,	1, 0) * chunkSize)].blocks;
		//adjacent[3] = map[pos + (new float3( 0,-1, 0) * chunkSize)].blocks;
		adjacent[2] = map[pos + (new float3( 0,	0, 0) * chunkSize)].blocks;
		adjacent[3] = map[pos + (new float3( 0, 0, 0) * chunkSize)].blocks;

		adjacent[4] = map[pos + (new float3( 0,	0, 1) * chunkSize)].blocks;
		adjacent[5] = map[pos + (new float3( 0,	0,-1) * chunkSize)].blocks;

		return adjacent;
	}

	delegate void GenerationStage(Vector3 position);
	void PositionsInSpiral(Vector3 center, int radius, GenerationStage ExecuteStage)
	{
		Vector3 position = center;

		//	Generate center block
		ExecuteStage(position);

		radius-=1;//DEBUG

		int increment = 1;
		for(int i = 0; i < radius; i++)
		{
			//	right then back
			for(int r = 0; r < increment; r++)
			{
				position += Vector3.right * chunkSize;
				ExecuteStage(position);
			}
			for(int b = 0; b < increment; b++)
			{
				position += Vector3.back * chunkSize;
				ExecuteStage(position);
			}

			increment++;

			//	left then forward
			for(int l = 0; l < increment; l++)
			{
				position += Vector3.left * chunkSize;
				ExecuteStage(position);
			}
			for(int f = 0; f < increment; f++)
			{
				position += Vector3.forward * chunkSize;
				ExecuteStage(position);
			}

			increment++;
		}
		//	Square made by spiral is always missing one corner
		for(int r = 0; r < increment - 1; r++)
		{
			position += Vector3.right * chunkSize;
		ExecuteStage(position);
		}
	}
}
