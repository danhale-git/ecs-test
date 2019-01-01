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
	//	Parralel job batch size
	int batchSize = 32;

	EntityManager entityManager;

	int cubeSize;
	int cubeArrayLength;

	public static Material material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/TestMaterial.mat");

	ArchetypeChunkEntityType 				entityType;
	ArchetypeChunkComponentType<Position> 	positionType;
	ArchetypeChunkComponentType<MapSquare>	squareType;
	ArchetypeChunkBufferType<MapCube> 		cubeType;
	ArchetypeChunkBufferType<Block> 		blocksType;
	ArchetypeChunkBufferType<MyComponents.Terrain> 		heightType;

	EntityArchetypeQuery squareQuery;

	protected override void OnCreateManager()
	{
		entityManager = World.Active.GetOrCreateManager<EntityManager>();

		cubeSize = TerrainSettings.cubeSize;
		cubeArrayLength = (int)math.pow(cubeSize, 3);

		squareQuery = new EntityArchetypeQuery{
			Any 	= Array.Empty<ComponentType>(),
			None  	= new ComponentType[] { typeof(Tags.InnerBuffer), typeof(Tags.OuterBuffer), typeof(Tags.SetBlocks) },
			All  	= new ComponentType[] { typeof(MapSquare), typeof(Tags.DrawMesh) }
			};
	}

	//	Query for meshes that need drawing
	protected override void OnUpdate()
	{
		entityType 		= GetArchetypeChunkEntityType();
		positionType 	= GetArchetypeChunkComponentType<Position>();
		squareType		= GetArchetypeChunkComponentType<MapSquare>();
		cubeType 		= GetArchetypeChunkBufferType<MapCube>();
		blocksType 		= GetArchetypeChunkBufferType<Block>();
        heightType = GetArchetypeChunkBufferType<MyComponents.Terrain>();

		NativeArray<ArchetypeChunk> chunks = entityManager.CreateArchetypeChunkArray(
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
			NativeArray<MapSquare>	squares			= chunk.GetNativeArray(squareType);
			BufferAccessor<MapCube> cubeAccessor 	= chunk.GetBufferAccessor(cubeType);
			BufferAccessor<Block> 	blockAccessor 	= chunk.GetBufferAccessor(blocksType);
            BufferAccessor<MyComponents.Terrain> heightAccessor	= chunk.GetBufferAccessor(heightType);

			//	Iterate over map square entities
			for(int e = 0; e < entities.Length; e++)
			{
				Entity entity = entities[e];
				float2 position = new float2(
					positions[e].Value.x,
					positions[e].Value.z
					);

				//	Get blocks from adjacent map squares
				AdjacentSquares adjacentSquares = entityManager.GetComponentData<AdjacentSquares>(entity);
				DynamicBuffer<Block>[] adjacentBlocks = new DynamicBuffer<Block>[4];
				for(int i = 0; i < 4; i++)
					adjacentBlocks[i] = entityManager.GetBuffer<Block>(adjacentSquares[i]);

                DynamicBuffer<MyComponents.Terrain>[] adjacentHeightMaps = new DynamicBuffer<MyComponents.Terrain>[8];
				for(int i = 0; i < 8; i++)
					adjacentHeightMaps[i] = entityManager.GetBuffer<MyComponents.Terrain>(adjacentSquares[i]);

				//	Get vertex offsets for slopes
				NativeArray<float> slopes = GetSlopes(heightAccessor[e].ToNativeArray(), adjacentHeightMaps, blockAccessor[e]);

				//	Check block face exposure
				int faceCount;
				NativeArray<Faces> faces = CheckBlockFaces(
					squares[e],
					adjacentBlocks,
					blockAccessor[e],
					cubeAccessor[e],
					out faceCount
					);


				//	Create mesh entity if any faces are exposed
				if(faceCount != 0)
					SetMeshComponent(
						GetMesh(faces, blockAccessor[e], heightAccessor[e].ToNativeArray(), slopes, faceCount),
						position,
						entity,
						commandBuffer
						);

				commandBuffer.RemoveComponent(entity, typeof(Tags.DrawMesh));
				faces.Dispose();
				slopes.Dispose();
			}
		}
		commandBuffer.Playback(entityManager);
		commandBuffer.Dispose();

		chunks.Dispose();
	}	

	//	Generate structs with int values showing face exposure for each block
	public NativeArray<Faces> CheckBlockFaces(MapSquare mapSquare, DynamicBuffer<Block>[] adjacentBlocks, DynamicBuffer<Block> blocks, DynamicBuffer<MapCube> cubes, out int faceCount)
	{
		var exposedFaces = new NativeArray<Faces>(blocks.Length, Allocator.TempJob);

		NativeArray<Block>[] adjacent = new NativeArray<Block>[] {
			new NativeArray<Block>(adjacentBlocks[0].Length, Allocator.TempJob),
			new NativeArray<Block>(adjacentBlocks[1].Length, Allocator.TempJob),
			new NativeArray<Block>(adjacentBlocks[2].Length, Allocator.TempJob),
			new NativeArray<Block>(adjacentBlocks[3].Length, Allocator.TempJob)
			};

		for(int i = 0; i < 4; i++)
			adjacent[i].CopyFrom(adjacentBlocks[i].ToNativeArray());

		int topCube 	= (int)math.floor((mapSquare.highestVisibleBlock + 1) / cubeSize);
		int bottomCube 	= (int)math.floor((mapSquare.lowestVisibleBlock - 1) / cubeSize);

		//	Leave a buffer of one to guarantee adjacent block data
		for(int i = bottomCube; i <= topCube; i++)
		{
			if(((i-1) * cubeArrayLength) < 0) Debug.Log("negative index!");

			var job = new FacesJob(){
				exposedFaces = exposedFaces,

				blocks 	= blocks.ToNativeArray(),
				right 	= adjacent[0],
				left 	= adjacent[1],
				front 	= adjacent[2],
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

	//	Generate list of Y offsets for top 4 cube vertices
	public NativeArray<float> GetSlopes(NativeArray<MyComponents.Terrain> heightMap, DynamicBuffer<MyComponents.Terrain>[] adjacentHeightMaps, DynamicBuffer<Block> blocks)
	{
		NativeArray<float> heightDifferences = new NativeArray<float>(heightMap.Length * 4, Allocator.TempJob);

		for(int h = 0; h < heightMap.Length; h++)
		{
			int height = heightMap[h].height;

			//	This position
			float3 pos = Util.Unflatten2D(h, cubeSize);

			float3[] directions = Util.CardinalDirections();

			//	Height differences for all adjacent positions
			float[] differences = new float[directions.Length];

			int changedVertexCount = 0;

			for(int i = 0; i < differences.Length; i++)
			{
				int xPos = (int)(directions[i].x + pos.x);
				int zPos = (int)(directions[i].z + pos.z);

				//	1 or -1 = beyond the edge of the square 
				float3 edge = new float3(
					xPos == cubeSize ? 1 : xPos < 0 ? -1 : 0,
					0,
					zPos == cubeSize ? 1 : zPos < 0 ? -1 : 0
					);

				int adjacentHeight;

				if(	edge.x != 0 || edge.z != 0)
					adjacentHeight = adjacentHeightMaps[Util.CardinalDirectionIndex(edge)][Util.WrapAndFlatten2D(xPos, zPos, cubeSize)].height;
				else
					adjacentHeight = heightMap[Util.Flatten2D(xPos, zPos, cubeSize)].height;

				int difference = adjacentHeight - height;

				if(difference != 0) changedVertexCount++;

				differences[i] = difference;
			}
			
			int startIndex = h*4;

			float frontRight = GetVertexOffset(differences[0], differences[2], differences[4]);	//	front right
			float backRight = GetVertexOffset(differences[0], differences[3], differences[6]);	//	back right
			float frontLeft = GetVertexOffset(differences[1], differences[2], differences[5]);	//	front left
			float backLeft = GetVertexOffset(differences[1], differences[3], differences[7]);	//	back left


			int blockIndex = Util.BlockIndex(new float3(pos.x, height, pos.z), cubeSize);
			Block block = blocks[blockIndex];

			SlopeType slopeType;
			SlopeFacing slopeFacing;

			//if(changedVertexCount == 1 && (frontLeft != 0 || backRight != 0))

			blocks[blockIndex] = new Block{
				index = block.index,
				type = block.type,
				squareLocalPosition = block.squareLocalPosition,

				frontRightSlope = frontRight,
				backRightSlope = backRight,
				frontLeftSlope = frontLeft,
				backLeftSlope = backLeft
			};
		}
		return heightDifferences;
	}

	float GetVertexOffset(float adjacent1, float adjacent2, float diagonal)
	{
		bool anyAboveOne = (adjacent1 > 1 || adjacent2 > 1 || diagonal > 1);
		bool bothAdjacentAboveZero = (adjacent1 > 0 && adjacent2 > 0);
		bool anyAdjacentAboveZero = (adjacent1 > 0 || adjacent2 > 0);

		if(bothAdjacentAboveZero && anyAboveOne) return 1;
		
		if(anyAdjacentAboveZero) return 0;

		return math.clamp(adjacent1 + adjacent2 + diagonal, -1, 0);
		
	}

	public Mesh GetMesh(NativeArray<Faces> faces, DynamicBuffer<Block> blocks, NativeArray<MyComponents.Terrain> heightMap, NativeArray<float> heightDifferences, int faceCount)
	{
		if(blocks.Length == 0)
		{
			Debug.Log("Tried to draw empty cube!");
		}
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
			heightMap	= heightMap,
			//heightDifferences = heightDifferences,

			util 		= new JobUtil(),
			cubeSize 	= cubeSize,

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

		UnityEditor.MeshUtility.Optimize(mesh);
		mesh.RecalculateNormals();

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
} 