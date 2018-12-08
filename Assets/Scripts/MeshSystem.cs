using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

using UnityEngine;
using UnityEditor;

[UpdateAfter(typeof(BlockSystem))]
public class MeshSystem : ComponentSystem
{
	EntityManager entityManager;
	ArchetypeChunkEntityType entityType;
	EntityArchetype meshArchetype;

	public static Material material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/TestMaterial.mat");

	ArchetypeChunkComponentType<MapChunk> chunkType;
	EntityArchetypeQuery meshQuery;

	int chunkSize;

	float3 worldStartPosition;

	protected override void OnCreateManager()
	{
		entityManager = World.Active.GetOrCreateManager<EntityManager>();
		chunkSize = MapChunkSystem.chunkSize;

		//	Archetype for mesh entities
		meshArchetype = entityManager.CreateArchetype
			(
			ComponentType.Create<Position>(), 
			ComponentType.Create<MeshInstanceRendererComponent>()  
			);

		//	Chunks with block data and no mesh
		meshQuery = new EntityArchetypeQuery
		{
			Any = Array.Empty<ComponentType>(),
			None = new ComponentType[] { typeof(MESH), typeof(MapEdge) },
			All = new ComponentType[] { typeof(MapChunk), typeof(BLOCKS) }
		};
	}
	
	protected override void OnUpdate()
	{
		entityType = GetArchetypeChunkEntityType();
		chunkType = GetArchetypeChunkComponentType<MapChunk>();

		NativeArray<ArchetypeChunk> dataChunks = entityManager.CreateArchetypeChunkArray(meshQuery, Allocator.TempJob);
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
			var chunks = dataChunk.GetNativeArray(chunkType);

			//	Native arrays much be used as use of entityManager invalidates the 'entities' array
			int entitiesLength = entities.Length;
            NativeArray<Entity> entityArray = new NativeArray<Entity>(entitiesLength, Allocator.TempJob);
            entityArray.CopyFrom(entities);
            NativeArray<MapChunk> chunkArray = new NativeArray<MapChunk>(chunks.Length, Allocator.TempJob);
            chunkArray.CopyFrom(chunks);

			for(int e = 0; e < entities.Length; e++)
			{
				var ourChunkEntity = entityArray[e];
				var chunkWorldPosition = chunkArray[e].worldPosition;

				//	Check block face exposure
				Entity[] adjacentChunks = MapChunkSystem.GetAdjacentSquares(chunkWorldPosition);
				int faceCount;
				Faces[] facesArray = GetFaces(
					16,
					adjacentChunks,
					entityManager.GetBuffer<Block>(ourChunkEntity),
					out faceCount
					);

				NativeArray<Faces> faces = new NativeArray<Faces>(facesArray.Length,
				Allocator.TempJob);
				faces.CopyFrom(facesArray);

				if(faceCount == 0) return;

				Entity meshEntity = entityManager.CreateEntity(meshArchetype);
                entityManager.SetComponentData(meshEntity, new Position { Value = chunkWorldPosition });

				Mesh mesh = GetMesh(16, faces, entityManager.GetBuffer<Block>(ourChunkEntity), faceCount);
				MeshInstanceRenderer renderer = new MeshInstanceRenderer();
        		renderer.mesh = mesh;
        		renderer.material = material;

		        entityManager.AddSharedComponentData(meshEntity, renderer);
	
				entityManager.AddComponent(ourChunkEntity, typeof(MESH));

				faces.Dispose();
			}

			entityArray.Dispose();
			chunkArray.Dispose();
			dataChunks.Dispose();
		}
	}

	public Faces[] GetFaces(int batchSize, Entity[] adjacentChunks, DynamicBuffer<Block> _blocks, out int faceCount)
	{
		var exposedFaces = new NativeArray<Faces>(_blocks.Length, Allocator.TempJob);
		Faces[] exposedFacesArray = new Faces[exposedFaces.Length];

		NativeArray<int>[] adjacent = new NativeArray<int>[] {
			new NativeArray<int>(_blocks.Length, Allocator.TempJob),
			new NativeArray<int>(_blocks.Length, Allocator.TempJob),
			new NativeArray<int>(_blocks.Length, Allocator.TempJob),
			new NativeArray<int>(_blocks.Length, Allocator.TempJob),
			new NativeArray<int>(_blocks.Length, Allocator.TempJob),
			new NativeArray<int>(_blocks.Length, Allocator.TempJob)
		};

		for(int i = 0; i < 6; i++)
		{
			DynamicBuffer<Block> buffer = entityManager.GetBuffer<Block>(adjacentChunks[i]);
			for(int b = 0; b < buffer.Length; b++)
				adjacent[i][b] = buffer[b].blockType;
		}

		var job = new CheckBlockFacesJob(){
			exposedFaces = exposedFaces,
			blocks = _blocks,
			chunkSize = chunkSize,
			util = new JobUtil(),

			right = adjacent[0],
			left = adjacent[1],
			up = adjacent[2],
			down = adjacent[3],
			forward = adjacent[4],
			back = adjacent[5]
			};
		
		//  Fill native array
        JobHandle jobHandle = job.Schedule(_blocks.Length, batchSize);
        jobHandle.Complete();

		for(int i = 0; i < 6; i++)
			adjacent[i].Dispose();

		exposedFaces.CopyTo(exposedFacesArray);
		exposedFaces.Dispose();

		faceCount = GetExposedBlockIndices(exposedFacesArray);

		return exposedFacesArray;
	}

	int GetExposedBlockIndices(Faces[] faces)
	{
		int faceCount = 0;
		for(int i = 0; i < faces.Length; i++)
		{
			int count = faces[i].count;
			if(count > 0)
			{
				faces[i].faceIndex = faceCount;
				faceCount += count;
			}
		}
		return faceCount;
	}


	public Mesh GetMesh(int batchSize, NativeArray<Faces> faces, DynamicBuffer<Block> blocks, int faceCount)
	{
		//	Determine vertex and triangle arrays using face count
		NativeArray<float3> vertices = new NativeArray<float3>(faceCount * 4, Allocator.TempJob);
		NativeArray<int> triangles = new NativeArray<int>(faceCount * 6, Allocator.TempJob);
		NativeArray<float4> colors = new NativeArray<float4>(faceCount * 4, Allocator.TempJob);

		var job = new GenerateMeshJob()
		{
			vertices = vertices,
			triangles = triangles,
			colors = colors,

			faces = faces,
			blocks = blocks,

			util = new JobUtil(),
			//meshGenerator = new MeshGenerator(0),
			chunkSize = chunkSize,

			baseVerts = new CubeVertices(true)
		};

		//	Run job
		JobHandle handle = job.Schedule(faces.Length, batchSize);
		handle.Complete();

		//	Vert(float3) native array to(Vector3) array
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