using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using MyComponents;

using UnityEngine;
using UnityEditor;

[UpdateAfter(typeof(BlockSystem))]
public class MeshSystem : ComponentSystem
{
	EntityManager entityManager;
	ArchetypeChunkEntityType entityType;
	EntityArchetype meshArchetype;

	public static Material material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/TestMaterial.mat");

	ArchetypeChunkComponentType<MapCube> cubeType;
	EntityArchetypeQuery cubeQuery;

	int cubeSize;

	float3 worldStartPosition;

	protected override void OnCreateManager()
	{
		entityManager = World.Active.GetOrCreateManager<EntityManager>();
		cubeSize = TerrainSettings.cubeSize;

		//	Archetype for mesh entities
		meshArchetype = entityManager.CreateArchetype
			(
			ComponentType.Create<Position>(), 
			ComponentType.Create<MeshInstanceRendererComponent>()  
			);

		//	Chunks with block data and no mesh
		cubeQuery = new EntityArchetypeQuery
		{
			Any = Array.Empty<ComponentType>(),
			None = Array.Empty<ComponentType>(),
			All = new ComponentType[] { typeof(MapCube), typeof(Tags.DrawMesh) }
		};
	}
	
	protected override void OnUpdate()
	{
		entityType = GetArchetypeChunkEntityType();
		cubeType = GetArchetypeChunkComponentType<MapCube>();

		NativeArray<ArchetypeChunk> dataChunks = entityManager.CreateArchetypeChunkArray(cubeQuery, Allocator.TempJob);
		if(dataChunks.Length == 0)
			dataChunks.Dispose();
		else
			ProcessChunks(dataChunks);
	}

	void ProcessChunks(NativeArray<ArchetypeChunk> dataChunks)
	{
		for(int d = 0; d < dataChunks.Length; d++)
		{
			var dataChunk = dataChunks[d];
			var entities = dataChunk.GetNativeArray(entityType);
			var cubes = dataChunk.GetNativeArray(cubeType);

			//	Native arrays much be used as use of entityManager invalidates the 'entities' array
			int entitiesLength = entities.Length;
            NativeArray<Entity> entityArray = new NativeArray<Entity>(entitiesLength, Allocator.Persistent);
            entityArray.CopyFrom(entities);
            NativeArray<MapCube> cubeArray = new NativeArray<MapCube>(cubes.Length, Allocator.Persistent);
            cubeArray.CopyFrom(cubes);

			for(int e = 0; e < entities.Length; e++)
			{
				var cubeEntity = entityArray[e];
				var cubeWorldPosition = cubeArray[e].worldPosition;

				//	Check block face exposure
				Entity[] adjacentChunks = MapCubeSystem.GetAdjacentSquares(cubeWorldPosition);
				int faceCount;
				NativeArray<Faces> faces = CheckBlockFaces(
					16,
					adjacentChunks,
					entityManager.GetBuffer<Block>(cubeEntity),
					out faceCount
					);

				//	Skip mesh entity if no exposed faces
				if(faceCount != 0)
				{
					Mesh mesh = GetMesh(16, faces, entityManager.GetBuffer<Block>(cubeEntity), faceCount);
					CreateMeshEntity(mesh, cubeWorldPosition);
				}

				entityManager.RemoveComponent(cubeEntity, typeof(Tags.DrawMesh));
				entityManager.AddComponent(cubeEntity, typeof(Tags.MeshDrawn));

				faces.Dispose();
			}

			entityArray.Dispose();
			cubeArray.Dispose();
			dataChunks.Dispose();
		}
	}

	//	Create entity with mesh at position
	void CreateMeshEntity(Mesh mesh, float3 chunkWorldPosition)
	{
		Entity meshEntity = entityManager.CreateEntity(meshArchetype);
		entityManager.SetComponentData(meshEntity, new Position { Value = chunkWorldPosition });
		
		MeshInstanceRenderer renderer = new MeshInstanceRenderer();
		renderer.mesh = mesh;
		renderer.material = material;

		entityManager.AddSharedComponentData(meshEntity, renderer);
	}

	public NativeArray<Faces> CheckBlockFaces(int batchSize, Entity[] adjacentChunks, DynamicBuffer<Block> _blocks, out int faceCount)
	{
		var exposedFaces = new NativeArray<Faces>(_blocks.Length, Allocator.TempJob);

		//	Block types for all adjacent chunks
		NativeArray<int>[] adjacent = new NativeArray<int>[] {
			new NativeArray<int>(_blocks.Length, Allocator.TempJob),
			new NativeArray<int>(_blocks.Length, Allocator.TempJob),
			new NativeArray<int>(_blocks.Length, Allocator.TempJob),
			new NativeArray<int>(_blocks.Length, Allocator.TempJob),
			new NativeArray<int>(_blocks.Length, Allocator.TempJob),
			new NativeArray<int>(_blocks.Length, Allocator.TempJob)
		};

		//	Get block types from buffers
		/*for(int i = 0; i < 6; i++)
		{
			DynamicBuffer<Block> buffer = entityManager.GetBuffer<Block>(adjacentChunks[i]);
			for(int b = 0; b < buffer.Length; b++)
				adjacent[i][b] = buffer[b].type;
		}*/

		var job = new BlockFacesJob(){
			exposedFaces = exposedFaces,
			blocks = _blocks,
			chunkSize = cubeSize,
			util = new JobUtil(),

			/*right = adjacent[0],
			left = adjacent[1],
			up = adjacent[2],
			down = adjacent[3],
			forward = adjacent[4],
			back = adjacent[5]*/
			};
		
        JobHandle jobHandle = job.Schedule(_blocks.Length, batchSize);
        jobHandle.Complete();

		//	Dispose of adjacent block type arrays
		for(int i = 0; i < 6; i++)
			adjacent[i].Dispose();

		//	Count total exposed faces and set face indices	
		faceCount = 0;
		for(int i = 0; i < exposedFaces.Length; i++)
		{
			int count = exposedFaces[i].count;
			if(count > 0)
			{
				Faces blockFaces = exposedFaces[i];
				blockFaces.faceIndex = faceCount;
				exposedFaces[i] = blockFaces;
				faceCount += count;
			}
		}

		return exposedFaces;
	}

	public Mesh GetMesh(int batchSize, NativeArray<Faces> faces, DynamicBuffer<Block> blocks, int faceCount)
	{
		//	Determine vertex and triangle arrays using face count
		NativeArray<float3> vertices = new NativeArray<float3>(faceCount * 4, Allocator.TempJob);
		NativeArray<int> triangles = new NativeArray<int>(faceCount * 6, Allocator.TempJob);
		NativeArray<float4> colors = new NativeArray<float4>(faceCount * 4, Allocator.TempJob);

		var job = new MeshJob()
		{
			vertices = vertices,
			triangles = triangles,
			colors = colors,

			faces = faces,
			blocks = blocks,

			util = new JobUtil(),
			chunkSize = cubeSize,

			baseVerts = new CubeVertices(true)
		};

		//	Run job
		JobHandle handle = job.Schedule(faces.Length, batchSize);
		handle.Complete();

		//	Convert vertices and colors from float3/float4 to Vector3/Color
		Vector3[] verticesArray = new Vector3[vertices.Length];
		Color[] colorsArray = new Color[colors.Length];
		for(int i = 0; i < vertices.Length; i++)
		{
			verticesArray[i] = vertices[i];
			colorsArray[i] = new Color
				(
					colors[i].x,
					colors[i].y,
					colors[i].z,
					colors[i].w
				);
		}

		//	Tri native array to array
		int[] trianglesArray = new int[triangles.Length];
		triangles.CopyTo(trianglesArray);
		
		vertices.Dispose();
		triangles.Dispose();
		colors.Dispose();

		return MakeMesh(verticesArray, trianglesArray, colorsArray);
	}

	Mesh MakeMesh(Vector3[] vertices, int[] triangles, Color[] colors)
	{
		Mesh mesh = new Mesh();
		mesh.vertices = vertices;
		mesh.SetTriangles(triangles, 0);
		mesh.colors = colors;
		mesh.RecalculateNormals();
		UnityEditor.MeshUtility.Optimize(mesh);

		return mesh;
	}
} 