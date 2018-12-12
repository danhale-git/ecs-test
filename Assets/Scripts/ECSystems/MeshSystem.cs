﻿using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using MyComponents;

using UnityEngine;
using UnityEditor;

[UpdateAfter(typeof(CubeSystem))]
public class MeshSystem : ComponentSystem
{
	//	Parralel job batch size
	int batchSize = 32;

	EntityManager 	entityManager;

	int cubeSize;
	int cubeArrayLength;

	public static Material material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/TestMaterial.mat");

	ArchetypeChunkEntityType 				entityType;
	ArchetypeChunkComponentType<MapSquare> 	squareType;
	ArchetypeChunkBufferType<MapCube> 		cubeType;
	ArchetypeChunkBufferType<Block> 		blocksType;

	EntityArchetypeQuery squareQuery;

	protected override void OnCreateManager()
	{
		entityManager = World.Active.GetOrCreateManager<EntityManager>();

		//	Get cube size dependant values
		cubeSize = TerrainSettings.cubeSize;
		cubeArrayLength = (int)math.pow(cubeSize, 3);

		//	Construct query
		squareQuery = new EntityArchetypeQuery
		{
			Any 	= Array.Empty<ComponentType>(),
			None 	= Array.Empty<ComponentType>(),
			All  	= new ComponentType[] { typeof(MapSquare), typeof(Tags.DrawMesh) }
		};
	}

	//	Query for meshes that need drawing
	protected override void OnUpdate()
	{
		entityType 	= GetArchetypeChunkEntityType();
		squareType	= GetArchetypeChunkComponentType<MapSquare>();
		cubeType 	= GetArchetypeChunkBufferType<MapCube>();
		blocksType 	= GetArchetypeChunkBufferType<Block>();

		NativeArray<ArchetypeChunk> chunks;
		chunks	= entityManager.CreateArchetypeChunkArray(
			squareQuery, Allocator.TempJob
			);

		if(chunks.Length == 0) chunks.Dispose();
		else DrawMesh(chunks);
			
	}

	//	Generate mesh and apply to entity
	void DrawMesh(NativeArray<ArchetypeChunk> chunks)
	{
		EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);

		for(int c = 0; c < chunks.Length; c++)
		{
			ArchetypeChunk chunk = chunks[c];

			//	Get chunk data
			NativeArray<Entity> 	entities 		= chunk.GetNativeArray(entityType);
			NativeArray<MapSquare> 	squares 		= chunk.GetNativeArray(squareType);
			BufferAccessor<MapCube> cubeAccessor 	= chunk.GetBufferAccessor(cubeType);
			BufferAccessor<Block> 	blockAccessor 	= chunk.GetBufferAccessor(blocksType);

			//	Iterate over map square entities
			for(int e = 0; e < entities.Length; e++)
			{
				var entity = entities[e];

				//	Check block face exposure
				Entity[] 	adjacentChunks = new Entity[] {};
				int 		faceCount;

				NativeArray<Faces> faces = CheckBlockFaces(
					adjacentChunks,
					blockAccessor[e],
					out faceCount,
					cubeAccessor[e]
					);

				//	Create mesh entity if any faces are exposed
				if(faceCount != 0)
					SetMeshComponent(
						GetMesh(faces, blockAccessor[e], faceCount),
						squares[e].position,
						entity,
						commandBuffer
						);

				commandBuffer.RemoveComponent(entity, typeof(Tags.DrawMesh));
				faces.Dispose();
			}

			commandBuffer.Playback(entityManager);
			commandBuffer.Dispose();

			chunks.Dispose();
		}
	}


	// Apply mesh to MapSquare entity
	void SetMeshComponent(Mesh mesh, float2 pos, Entity entity, EntityCommandBuffer commandBuffer)
	{
		MeshInstanceRenderer renderer = new MeshInstanceRenderer();
		renderer.mesh = mesh;
		renderer.material = material;

		commandBuffer.AddSharedComponent(entity, renderer);
	}

	//	Generate structs with int values showing face exposure for each block
	public NativeArray<Faces> CheckBlockFaces(Entity[] adjacentChunks, DynamicBuffer<Block> blocks, out int faceCount, DynamicBuffer<MapCube> cubes)
	{
		var exposedFaces = new NativeArray<Faces>(blocks.Length, Allocator.TempJob);

		//	Block types for all adjacent chunks
		NativeArray<int>[] adjacent = new NativeArray<int>[] {
			new NativeArray<int>(blocks.Length, Allocator.TempJob),
			new NativeArray<int>(blocks.Length, Allocator.TempJob),
			new NativeArray<int>(blocks.Length, Allocator.TempJob),
			new NativeArray<int>(blocks.Length, Allocator.TempJob),
			new NativeArray<int>(blocks.Length, Allocator.TempJob),
			new NativeArray<int>(blocks.Length, Allocator.TempJob)
		};

		for(int i = 0; i < cubes.Length; i++)
		{
			var job = new BlockFacesJob(){
				exposedFaces = exposedFaces,

				cubeStart = i * cubeArrayLength,
				cubePosY = cubes[i].yPos,
				blocks = blocks,

				chunkSize = cubeSize,
				util = new JobUtil(),

				/*right = adjacent[0],
				left = adjacent[1],
				up = adjacent[2],
				down = adjacent[3],
				forward = adjacent[4],
				back = adjacent[5]*/
				};
			
			job.Schedule(cubeArrayLength, batchSize).Complete();
		}

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

	public Mesh GetMesh(NativeArray<Faces> faces, DynamicBuffer<Block> blocks, int faceCount)
	{
		//	Determine vertex and triangle arrays using face count
		NativeArray<float3> vertices = new NativeArray<float3>(	faceCount * 4, Allocator.TempJob);
		NativeArray<int> triangles = new NativeArray<int>(		faceCount * 6, Allocator.TempJob);
		NativeArray<float4> colors = new NativeArray<float4>(	faceCount * 4, Allocator.TempJob);

		var job = new MeshJob(){
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
		job.Schedule(faces.Length, batchSize).Complete();

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
		Mesh mesh 		= new Mesh();
		mesh.vertices 	= vertices;
		mesh.colors 	= colors;
		mesh.SetTriangles(triangles, 0);

		mesh.RecalculateNormals();
		//UnityEditor.MeshUtility.Optimize(mesh);

		return mesh;
	}
} 