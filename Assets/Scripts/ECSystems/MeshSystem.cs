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
	//	Parralel job batch size
	int batchSize = 32;

	EntityManager 	entityManager;

	int cubeSize;
	int cubeArrayLength;

	public static Material material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/TestMaterial.mat");

	ArchetypeChunkEntityType 				entityType;
	ArchetypeChunkComponentType<Position> 	positionType;
	ArchetypeChunkBufferType<MapCube> 		cubeType;
	ArchetypeChunkBufferType<Block> 		blocksType;

	EntityArchetypeQuery squareQuery;
	EntityArchetypeQuery bufferQuery;

	protected override void OnCreateManager()
	{
		entityManager = World.Active.GetOrCreateManager<EntityManager>();

		//	Get cube size dependant values
		cubeSize = TerrainSettings.cubeSize;
		cubeArrayLength = (int)math.pow(cubeSize, 3);

		//	Construct query
		squareQuery = new EntityArchetypeQuery{
			Any 	= Array.Empty<ComponentType>(),
			None  	= new ComponentType[] { typeof(Tags.InnerBuffer), typeof(Tags.OuterBuffer), typeof(Tags.GenerateBlocks) },
			All  	= new ComponentType[] { typeof(MapSquare), typeof(Tags.DrawMesh) }
			};

		//	Construct query
		bufferQuery = new EntityArchetypeQuery{
			Any 	= Array.Empty<ComponentType>(),
			None  	= new ComponentType[] { typeof(Tags.GenerateBlocks) },
			All  	= new ComponentType[] { typeof(MapSquare), }
			};
	}

	//	Query for meshes that need drawing
	protected override void OnUpdate()
	{
		entityType 		= GetArchetypeChunkEntityType();
		positionType 	= GetArchetypeChunkComponentType<Position>();
		cubeType 		= GetArchetypeChunkBufferType<MapCube>();
		blocksType 		= GetArchetypeChunkBufferType<Block>();

		NativeArray<ArchetypeChunk> chunks;
		chunks	= entityManager.CreateArchetypeChunkArray(
			squareQuery,
			Allocator.TempJob
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
			NativeArray<Position> 	positions		= chunk.GetNativeArray(positionType);
			BufferAccessor<MapCube> cubeAccessor 	= chunk.GetBufferAccessor(cubeType);
			BufferAccessor<Block> 	blockAccessor 	= chunk.GetBufferAccessor(blocksType);


			//	Iterate over map square entities
			for(int e = 0; e < entities.Length; e++)
			{
				//	Get all adjacent blocks and skip if any are missing
				DynamicBuffer<Block>[] adjacentBlocks;
				if(!GetAdjacentBuffers(positions[e].Value, out adjacentBlocks))
				{
					CustomDebugTools.SetWireCubeChunk(positions[e].Value, cubeSize -1, Color.red);
					//throw new System.IndexOutOfRangeException(
					//	"GetAdjacentBuffers did not find adjacent squares at "+positions[e].Value
					//	);
				}

				Entity entity = entities[e];
				float2 position = new float2(
					positions[e].Value.x,
					positions[e].Value.z
					);

				//	Check block face exposure
				int faceCount;

				NativeArray<Faces> faces = CheckBlockFaces(
					adjacentBlocks,
					blockAccessor[e],
					out faceCount,
					cubeAccessor[e]
					);


				//	Create mesh entity if any faces are exposed
				if(faceCount != 0)
					SetMeshComponent(
						GetMesh(faces, blockAccessor[e], faceCount),
						position,
						entity,
						commandBuffer
						);

				commandBuffer.RemoveComponent(entity, typeof(Tags.DrawMesh));
				faces.Dispose();
			}
		}
		commandBuffer.Playback(entityManager);
		commandBuffer.Dispose();

		chunks.Dispose();
	}	

	//	Generate structs with int values showing face exposure for each block
	public NativeArray<Faces> CheckBlockFaces(DynamicBuffer<Block>[] adjacentBlocks, DynamicBuffer<Block> blocks, out int faceCount, DynamicBuffer<MapCube> cubes)
	{
		var exposedFaces = new NativeArray<Faces>(blocks.Length, Allocator.TempJob);

		NativeArray<Block>[] adjacent = new NativeArray<Block>[] {
			new NativeArray<Block>(adjacentBlocks[0].Length, Allocator.TempJob),
			new NativeArray<Block>(adjacentBlocks[0].Length, Allocator.TempJob),
			new NativeArray<Block>(adjacentBlocks[0].Length, Allocator.TempJob),
			new NativeArray<Block>(adjacentBlocks[0].Length, Allocator.TempJob)
			};

		for(int i = 0; i < 4; i++)
		{
			adjacent[i].CopyFrom(adjacentBlocks[i].ToNativeArray());
		}

		//	Leave a buffer of one to guarantee adjacent block data
		for(int i = 1; i < cubes.Length-1; i++)
		{
			var job = new FacesJob(){
				exposedFaces = exposedFaces,

				blocks 	= blocks,
				right 	= adjacent[0],
				left 	= adjacent[1],
				forward = adjacent[2],
				back 	= adjacent[3],

				cubeStart 	= (i  ) * cubeArrayLength,
				aboveStart 	= (i+1) * cubeArrayLength,
				belowStart 	= (i-1) * cubeArrayLength,
				
				cubeSize 	= cubeSize,
				util 		= new JobUtil()
				};
			
			job.Schedule(cubeArrayLength, batchSize).Complete();
		}

		for(int i = 0; i < 4; i++)
		{
			adjacent[i].Dispose();
		}

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
		NativeArray<float3> vertices 	= new NativeArray<float3>(faceCount * 4, Allocator.TempJob);
		NativeArray<int> triangles 		= new NativeArray<int>	 (faceCount * 6, Allocator.TempJob);
		NativeArray<float4> colors 		= new NativeArray<float4>(faceCount * 4, Allocator.TempJob);

		var job = new MeshJob(){
			vertices 	= vertices,
			triangles 	= triangles,
			colors 		= colors,

			faces 		= faces,
			blocks 		= blocks,

			util 		= new JobUtil(),
			chunkSize 	= cubeSize,

			baseVerts 	= new CubeVertices(true)
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

	// Apply mesh to MapSquare entity
	void SetMeshComponent(Mesh mesh, float2 pos, Entity entity, EntityCommandBuffer commandBuffer)
	{
		MeshInstanceRenderer renderer = new MeshInstanceRenderer();
		renderer.mesh = mesh;
		renderer.material = material;

		commandBuffer.AddSharedComponent(entity, renderer);
	}

	//	Get multiple map squares by positions
	//	TODO if we can't find 4 adjacent cause blocks aren't generated. just skip it
	bool GetAdjacentBuffers(float3 centerPosition, out DynamicBuffer<Block>[] buffers)
	{
		float3[] adjacentPositions = new float3[4] {
			centerPosition + (new float3( 1,  0,  0) * cubeSize),
			centerPosition + (new float3(-1,  0,  0) * cubeSize),
			centerPosition + (new float3( 0,  0,  1) * cubeSize),
			centerPosition + (new float3( 0,  0, -1) * cubeSize)
		};

		buffers = new DynamicBuffer<Block>[4];

		NativeArray<ArchetypeChunk> chunks = entityManager.CreateArchetypeChunkArray(
			bufferQuery,
			Allocator.TempJob
			);

		if(chunks.Length == 0)
		{
			chunks.Dispose();
			return false;
		}

		int count = 0;
		for(int d = 0; d < chunks.Length; d++)
		{
			ArchetypeChunk chunk = chunks[d];

			NativeArray<Entity> entities 	= chunk.GetNativeArray(entityType);
			NativeArray<Position> positions = chunk.GetNativeArray(positionType);
			BufferAccessor<Block> blocks	= chunk.GetBufferAccessor(blocksType);

			for(int e = 0; e < positions.Length; e++)
			{

				for(int p = 0; p < 4; p++)
				{
					float3 position = adjacentPositions[p];
					if(	position.x == positions[e].Value.x &&
						position.z == positions[e].Value.z)
					{
						buffers[p] = blocks[e];
						count++;

						if(count == 4)
						{
							chunks.Dispose();
							return true;
						}
					}
				}
			}
		}

		chunks.Dispose();
		return false;
	}
} 