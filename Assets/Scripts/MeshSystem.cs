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
	//Util util;
	EntityManager entityManager;
	ArchetypeChunkEntityType entityType;
	EntityArchetype meshArchetype;

	public static Material material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/TestMaterial.mat");

	ComponentType chunkType;
	ComponentType blockType;
	ComponentType meshType;

	ArchetypeChunkComponentType<Chunk> ChunkType;
	EntityArchetypeQuery newChunkQuery;

	int chunkSize;

	float3 worldStartPosition;

	protected override void OnCreateManager ()
	{
		entityManager = World.Active.GetOrCreateManager<EntityManager> ();

		chunkSize = ChunkSystem.chunkSize;

		meshArchetype = entityManager.CreateArchetype
			(
			ComponentType.Create<Position> (), 
			ComponentType.Create<MeshInstanceRendererComponent> ()  
			);

		newChunkQuery = new EntityArchetypeQuery
		{
			Any = Array.Empty<ComponentType> (),
			None = new ComponentType [] { typeof(MESH) },
			All = new ComponentType [] { typeof(Chunk), typeof(BLOCKS) }
		};
	}
	
	protected override void OnUpdate ()
	{
		entityType = GetArchetypeChunkEntityType ();
		ChunkType = GetArchetypeChunkComponentType<Chunk> ();

		NativeArray<ArchetypeChunk> dataChunks = entityManager.CreateArchetypeChunkArray (newChunkQuery, Allocator.TempJob);
		if (dataChunks.Length == 0)
		{
			dataChunks.Dispose ();
		}
		else
		{
			ProcessChunks (dataChunks);
		}
	}

	void ProcessChunks(NativeArray<ArchetypeChunk> dataChunks)
	{
		//EntityCommandBuffer eCBuffer = new EntityCommandBuffer (Allocator.Temp);
		int entitiesLength;

		for (int d = 0; d < dataChunks.Length; d++)
		{
			var dataChunk = dataChunks [d];
			var entities = dataChunk.GetNativeArray (entityType);
			var chunks = dataChunk.GetNativeArray (ChunkType);

			entitiesLength = entities.Length;
            NativeArray<Entity> entityArray = new NativeArray<Entity> (entitiesLength, Allocator.TempJob);
            entityArray.CopyFrom (entities);
            NativeArray<Chunk> chunkArray = new NativeArray<Chunk> (chunks.Length, Allocator.TempJob);
            chunkArray.CopyFrom (chunks);

			for (int e = 0; e < entities.Length; e++)
			{
				var ourChunkEntity = entityArray [e];
				var ourChunk = chunkArray [e];
				var chunkWorldPosition = ourChunk.worldPosition;

				DynamicBuffer<Block> blockBuffer = entityManager.GetBuffer<Block> (ourChunkEntity);

				Entity meshEntity = entityManager.CreateEntity (meshArchetype);
                entityManager.SetComponentData (meshEntity, new Position { Value = chunkWorldPosition });

				//int requiredBlockArraySize = chunkSize * chunkSize * chunkSize;

				int faceCount;
				Faces[] facesArray = GetFaces(16, entityManager.GetBuffer<Block> (ourChunkEntity), out faceCount);
				NativeArray<Faces> faces = new NativeArray<Faces>(facesArray.Length, Allocator.TempJob);
				faces.CopyFrom(facesArray);

				if(faceCount == 0) return;

				Mesh mesh = GetMesh(16, faces, faceCount);
				MeshInstanceRenderer renderer = new MeshInstanceRenderer();
        		renderer.mesh = mesh;
        		renderer.material = material;

		        entityManager.AddSharedComponentData(meshEntity, renderer);
	
				entityManager.AddComponent (ourChunkEntity, typeof (MESH));

				faces.Dispose();
			}

			//eCBuffer.Playback (entityManager);
			//eCBuffer.Dispose ();

			entityArray.Dispose();
			chunkArray.Dispose();
			dataChunks.Dispose ();
		}
	}


	public Faces[] GetFaces(int batchSize, /*int[][] _adjacent,*/ DynamicBuffer<Block> _blocks, out int faceCount)
	{
		var exposedFaces = new NativeArray<Faces>(_blocks.Length, Allocator.TempJob);
		Faces[] exposedFacesArray = new Faces[exposedFaces.Length];

		/*NativeArray<int>[] adjacent = new NativeArray<int>[] {
			new NativeArray<int>(_blocks.Length, Allocator.TempJob),
			new NativeArray<int>(_blocks.Length, Allocator.TempJob),
			new NativeArray<int>(_blocks.Length, Allocator.TempJob),
			new NativeArray<int>(_blocks.Length, Allocator.TempJob),
			new NativeArray<int>(_blocks.Length, Allocator.TempJob),
			new NativeArray<int>(_blocks.Length, Allocator.TempJob)
		};

		for(int i = 0; i < 6; i++)
			adjacent[i].CopyFrom(_adjacent[i]);*/

		var job = new CheckBlockFacesJob(){
			exposedFaces = exposedFaces,
			blocks = _blocks,
			chunkSize = chunkSize,
			util = new JobUtil(),

			/*right = adjacent[0],
			left = adjacent[1],
			up = adjacent[2],
			down = adjacent[3],
			forward = adjacent[4],
			back = adjacent[5]*/
			};
		
		//  Fill native array
        JobHandle jobHandle = job.Schedule(_blocks.Length, batchSize);
        jobHandle.Complete();

		/*for(int i = 0; i < 6; i++)
			adjacent[i].Dispose();*/

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


	public Mesh GetMesh(int batchSize, NativeArray<Faces> faces, int faceCount)
	{
		//	Determine vertex and triangle arrays using face count
		NativeArray<float3> vertices = new NativeArray<float3>(faceCount * 4, Allocator.TempJob);
		NativeArray<int> triangles = new NativeArray<int>(faceCount * 6, Allocator.TempJob);

		var job = new GenerateMeshJob()
		{
			vertices = vertices,
			triangles = triangles,
			faces = faces,

			util = new JobUtil(),
			//meshGenerator = new MeshGenerator(0),
			chunkSize = chunkSize,

			baseVerts = new CubeVertices(true)
		};

		//	Run job
		JobHandle handle = job.Schedule(faces.Length, batchSize);
		handle.Complete();

		//	Vert (float3) native array to (Vector3) array
		Vector3[] verticesArray = new Vector3[vertices.Length];
		for(int i = 0; i < vertices.Length; i++)
			verticesArray[i] = vertices[i];

		//	Tri native array to array
		int[] trianglesArray = new int[triangles.Length];
		triangles.CopyTo(trianglesArray);
		
		vertices.Dispose();
		triangles.Dispose();

		return MakeMesh(verticesArray, trianglesArray);
	}

	Mesh MakeMesh(Vector3[] vertices, int[] triangles)
	{
		Mesh mesh = new Mesh();
		mesh.vertices = vertices;
		mesh.SetTriangles(triangles, 0);
		mesh.RecalculateNormals();
		UnityEditor.MeshUtility.Optimize(mesh);
		//mesh.RecalculateNormals();

		return mesh;
	}
} 