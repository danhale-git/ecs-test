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

[UpdateAfter(typeof(CubeSystem))]
public class MeshSystem : ComponentSystem
{
	EntityManager entityManager;
	EntityArchetype meshArchetype;

	int cubeSize;

	public static Material material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/TestMaterial.mat");

	ArchetypeChunkEntityType entityType;

	ArchetypeChunkComponentType<MapSquare> mapSquareType;

	ArchetypeChunkBufferType<Block> blocksType;
	ArchetypeChunkBufferType<CubePosition> cubePosType;

	EntityArchetypeQuery mapSquareQuery;

	protected override void OnCreateManager()
	{
		entityManager = World.Active.GetOrCreateManager<EntityManager>();
		cubeSize = TerrainSettings.cubeSize;

		mapSquareQuery = new EntityArchetypeQuery
		{
			Any 	= Array.Empty<ComponentType>(),
			None 	= Array.Empty<ComponentType>(),
			All  	= new ComponentType[] { typeof(MapSquare), typeof(Tags.DrawMesh) }
		};

		//	Archetype for mesh entities
		meshArchetype = entityManager.CreateArchetype
			(
			ComponentType.Create<Position>(), 
			ComponentType.Create<MeshInstanceRendererComponent>()  
			);
	}

	protected override void OnUpdate()
	{
		entityType 		= GetArchetypeChunkEntityType();

		mapSquareType	= GetArchetypeChunkComponentType<MapSquare>();

		blocksType 		= GetArchetypeChunkBufferType<Block>();
		cubePosType 	= GetArchetypeChunkBufferType<CubePosition>();

		NativeArray<ArchetypeChunk> chunks;
		chunks	= entityManager.CreateArchetypeChunkArray(
						mapSquareQuery, Allocator.TempJob
						);

		if(chunks.Length == 0) chunks.Dispose();
		else DrawMesh(chunks);
			
	}

	void DrawMesh(NativeArray<ArchetypeChunk> chunks)
	{
		EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);

		for(int c = 0; c < chunks.Length; c++)
		{
			ArchetypeChunk chunk = chunks[c];
			NativeArray<Entity> _entities 					= chunk.GetNativeArray(entityType);

			NativeArray<MapSquare> _mapSquares 				= chunk.GetNativeArray(mapSquareType);

			//BufferAccessor<Block> blockAccessor 			= chunk.GetBufferAccessor(blocksType);
			//BufferAccessor<CubePosition> cubePosAccessor 	= chunk.GetBufferAccessor(cubePosType);


			NativeArray<Entity> entities 					= new NativeArray<Entity>(_entities.Length, Allocator.TempJob);
			entities.CopyFrom(_entities);

			NativeArray<MapSquare> mapSquares 				= new NativeArray<MapSquare>(_mapSquares.Length, Allocator.TempJob);
			mapSquares.CopyFrom(_mapSquares);

			//BufferAccessor<Block> blockAccessor 			= new NativeArray<Block>();
			//BufferAccessor<CubePosition> cubePosAccessor 	= new NativeArray<CubePosition>();

			for(int e = 0; e < entities.Length; e++)
			{
				//	TODO add nativeArray wrapper
				var entity = entities[e];
				float3 cubeWorldPosition 	= new float3(
												mapSquares[e].worldPosition.x,
												0,
												mapSquares[e].worldPosition.y
												);

				DynamicBuffer<Block> blockBuffer		= entityManager.GetBuffer<Block>(entity);
				DynamicBuffer<CubePosition> cubes		= entityManager.GetBuffer<CubePosition>(entity);

				//	Check block face exposure
				Entity[] adjacentChunks = MapCubeSystem.GetAdjacentSquares(cubeWorldPosition);
				int faceCount;
				NativeArray<Faces> faces = CheckBlockFaces(
					16,
					adjacentChunks,
					//blockAccessor[e],
					blockBuffer,
					out faceCount,
					cubes.ToNativeArray()
					);

				//	Skip mesh entity if no exposed faces
				if(faceCount != 0)
				{
					Mesh mesh = GetMesh(16, faces, blockBuffer/*blockAccessor[e]*/, faceCount);
					CreateMeshEntity(mesh, cubeWorldPosition);
				}

				entityManager.RemoveComponent(entity, typeof(Tags.DrawMesh));

				faces.Dispose();
			}

			commandBuffer.Playback(entityManager);
			commandBuffer.Dispose();

			mapSquares.Dispose();
			entities.Dispose();

			chunks.Dispose();
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

	public NativeArray<Faces> CheckBlockFaces(int batchSize, Entity[] adjacentChunks, DynamicBuffer<Block> _blocks, out int faceCount, NativeArray<CubePosition> cubes)
	{
		var exposedFaces = new NativeArray<Faces>(_blocks.Length, Allocator.TempJob);

		int singleCubeArrayLength = (int)math.pow(cubeSize, 3);

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

		for(int i = 0; i < cubes.Length; i++)
		{
			var job = new BlockFacesJob(){
				exposedFaces = exposedFaces,

				cubeStart = i * singleCubeArrayLength,
				cubePosY = cubes[i].y,

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
			
			job.Schedule(singleCubeArrayLength, batchSize).Complete();
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