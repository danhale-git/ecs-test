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
				GetSlopes(heightAccessor[e].ToNativeArray(), adjacentHeightMaps, blockAccessor[e], positions[e]);

				//	Check block face exposure
				int faceCount;
				int vertCount;
				int triCount;
				NativeArray<Faces> faces = CheckBlockFaces(
					squares[e],
					adjacentBlocks,
					blockAccessor[e],
					cubeAccessor[e],
					out faceCount,
					out vertCount,
					out triCount
					);


				//	Create mesh entity if any faces are exposed
				if(faceCount != 0)
					SetMeshComponent(
						GetMesh(faces, blockAccessor[e], heightAccessor[e].ToNativeArray(), faceCount, vertCount, triCount),
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
	public NativeArray<Faces> CheckBlockFaces(MapSquare mapSquare, DynamicBuffer<Block>[] adjacentBlocks, DynamicBuffer<Block> blocks, DynamicBuffer<MapCube> cubes, out int faceCount, out int vertCount, out int triCount)
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

				cubeHeight	= i * cubeSize,
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
		vertCount = 0;
		triCount = 0;
		for(int i = 0; i < exposedFaces.Length; i++)
		{
			int count = exposedFaces[i].count;
			if(count > 0)
			{
				Faces blockFaces = exposedFaces[i];

				blockFaces.faceIndex = faceCount;
				blockFaces.vertIndex = vertCount;
				blockFaces.triIndex = triCount;

				exposedFaces[i] = blockFaces;

				//	Count verts
				vertCount += count * 4;
				//	Slopes have two extra verts
				if(blocks[i].slopeType != SlopeType.NOTSLOPED) vertCount += 2;

				//	Count tris because they have an arbitrary ratio to verts
				triCount += count * 6;

				faceCount += count;
			}
		}

		return exposedFaces;
	}

	//	Generate list of Y offsets for top 4 cube vertices
	void GetSlopes(NativeArray<MyComponents.Terrain> heightMap, DynamicBuffer<MyComponents.Terrain>[] adjacentHeightMaps, DynamicBuffer<Block> blocks, Position squarePosition)
	{
		//int slopeCount = 0;
		float3[] directions = Util.CardinalDirections();
		for(int h = 0; h < heightMap.Length; h++)
		{
			int height = heightMap[h].height;

			//	This position
			float3 pos = Util.Unflatten2D(h, cubeSize);

			int blockIndex = Util.BlockIndex(new float3(pos.x, height, pos.z), cubeSize);
			Block block = blocks[blockIndex];

			//	Block type is not sloped
			if(BlockTypes.sloped[block.type] == 0) continue;

			//	Height differences for all adjacent positions
			float[] differences = new float[directions.Length];

			int heightDifferenceCount = 0;

			for(int i = 0; i < differences.Length; i++)
			{
				int xPos = (int)(directions[i].x + pos.x);
				int zPos = (int)(directions[i].z + pos.z);

				//	Direction of the adjacent map square that owns the required block
				//	Zero if required block is in this map square 
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

				if(difference != 0) heightDifferenceCount++;

				differences[i] = difference;
			}

			//	Terrain is not sloped
			if(heightDifferenceCount == 0) continue;
			
			//	Get vertex offsets (-1 to 1) for top vertices of cube required for slope.
			float frontRight = GetVertexOffset(differences[0], differences[2], differences[4]);	//	front right
			float backRight = GetVertexOffset(differences[0], differences[3], differences[6]);	//	back right
			float frontLeft = GetVertexOffset(differences[1], differences[2], differences[5]);	//	front left
			float backLeft = GetVertexOffset(differences[1], differences[3], differences[7]);	//	back left

			int changedVertexCount = 0;

			if(frontRight != 0) changedVertexCount++;
			if(backRight != 0) changedVertexCount++;
			if(frontLeft != 0) changedVertexCount++;
			if(backLeft != 0) changedVertexCount++;

			SlopeType slopeType = 0;
			SlopeFacing slopeFacing = 0;

			//	Check slope type and facing axis
			if(changedVertexCount == 1 && (frontLeft != 0 || backRight != 0))
			{
				slopeType = SlopeType.INNERCORNER;	//	NWSE
				slopeFacing = SlopeFacing.NWSE;
			}
			else if(changedVertexCount == 1 && (frontRight != 0 || backLeft != 0))
			{
				slopeType = SlopeType.INNERCORNER;	//	SWNE
				slopeFacing = SlopeFacing.SWNE;
			}
			else if(frontRight < 0 && backLeft < 0)
			{
				slopeType = SlopeType.OUTERCORNER;
				slopeFacing = SlopeFacing.NWSE;
			}
			else if(frontLeft < 0 && backRight < 0)
			{
				slopeType = SlopeType.OUTERCORNER;
				slopeFacing = SlopeFacing.SWNE;
			}
			else
			{
				//	Don't need slope facing for flat slopes, only for corners
				slopeType = SlopeType.FLAT;
			}

			int debug = 0;
			/*if(squarePosition.Value.x + block.localPosition.x == -31 && squarePosition.Value.z + block.localPosition.z == 679)
			{
			} */

			blocks[blockIndex] = new Block{
				debug = debug,
				type = block.type,
				localPosition = block.localPosition,

				frontRightSlope = frontRight,
				backRightSlope = backRight,
				frontLeftSlope = frontLeft,
				backLeftSlope = backLeft,

				slopeType = slopeType,
				slopeFacing = slopeFacing
			};
		}
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

	public Mesh GetMesh(NativeArray<Faces> faces, DynamicBuffer<Block> blocks, NativeArray<MyComponents.Terrain> heightMap, int faceCount, int vertCount, int triCount)
	{
		if(blocks.Length == 0)
		{
			Debug.Log("Tried to draw empty cube!");
		}
		//	Determine vertex and triangle arrays using face count
		NativeArray<float3> vertices 	= new NativeArray<float3>(vertCount, Allocator.TempJob);
		NativeArray<float3> normals 	= new NativeArray<float3>(vertCount, Allocator.TempJob);
		NativeArray<int> triangles 		= new NativeArray<int>	 (triCount, Allocator.TempJob);
		NativeArray<float4> colors 		= new NativeArray<float4>(vertCount, Allocator.TempJob);

		MeshJob job = new MeshJob(){
			vertices 	= vertices,
			normals 	= normals,
			triangles 	= triangles,
			colors 		= colors,

			faces 		= faces,
			blocks 		= blocks,
			heightMap	= heightMap,

			util 		= new JobUtil(),
			cubeSize 	= cubeSize,

			baseVerts 	= new CubeVertices(true)
		};

		//	Run job
		job.Schedule(faces.Length, batchSize).Complete();

		//	Convert vertices and colors from float3/float4 to Vector3/Color
		Vector3[] verticesArray = new Vector3[vertices.Length];
		Vector3[] normalsArray = new Vector3[vertices.Length];
		Color[] colorsArray = new Color[colors.Length];
		for(int i = 0; i < vertices.Length; i++)
		{
			verticesArray[i] = vertices[i];
			normalsArray[i] = normals[i];

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
		normals.Dispose();
		triangles.Dispose();
		colors.Dispose();

		return MakeMesh(verticesArray, normalsArray, trianglesArray, colorsArray);
	}

	Mesh MakeMesh(Vector3[] vertices, Vector3[] normals, int[] triangles, Color[] colors)
	{
		Mesh mesh 		= new Mesh();
		mesh.vertices 	= vertices;
		mesh.normals 	= normals;
		mesh.colors 	= colors;
		mesh.SetTriangles(triangles, 0);

		//UnityEditor.MeshUtility.Optimize(mesh);
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