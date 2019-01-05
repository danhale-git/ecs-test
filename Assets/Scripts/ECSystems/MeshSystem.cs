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
	ArchetypeChunkBufferType<Block> 		blocksType;
	ArchetypeChunkBufferType<Topology> 		heightType;

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
		blocksType 		= GetArchetypeChunkBufferType<Block>();
        heightType = GetArchetypeChunkBufferType<Topology>();

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
			BufferAccessor<Block> 	blockAccessor 	= chunk.GetBufferAccessor(blocksType);
            BufferAccessor<Topology> heightAccessor	= chunk.GetBufferAccessor(heightType);

			//	Iterate over map square entities
			for(int e = 0; e < entities.Length; e++)
			{
				Entity entity = entities[e];
				float2 position = new float2(
					positions[e].Value.x,
					positions[e].Value.z
					);

				//	List of adjacent square entities
				AdjacentSquares adjacentSquares = entityManager.GetComponentData<AdjacentSquares>(entity);
				NativeArray<int> adjacentLowestBlocks = new NativeArray<int>(8, Allocator.TempJob);

				//	Adjacent blocks in 4 directions
				DynamicBuffer<Block>[] adjacentBlocks = new DynamicBuffer<Block>[4];
				for(int i = 0; i < 4; i++)
					adjacentBlocks[i] = entityManager.GetBuffer<Block>(adjacentSquares[i]);

				for(int i = 0; i < 8; i++)
					adjacentLowestBlocks[i] = entityManager.GetComponentData<MapSquare>(adjacentSquares[i]).bottomBuffer;


				//	Adjacent height maps in 8 directions
                DynamicBuffer<Topology>[] adjacentHeightMaps = new DynamicBuffer<Topology>[8];
				for(int i = 0; i < 8; i++)
					adjacentHeightMaps[i] = entityManager.GetBuffer<Topology>(adjacentSquares[i]);

				//	Vertex offsets for 4 top vertices of each block (slopes)
				//GetSlopes(heightAccessor[e].ToNativeArray(), adjacentHeightMaps, blockAccessor[e], positions[e]);

				//	Check block face exposure
				int faceCount;
				int vertCount;
				int triCount;
				NativeArray<Faces> faces = CheckBlockFaces(
					squares[e],
					adjacentSquares,
					adjacentLowestBlocks,
					adjacentBlocks,
					blockAccessor[e],
					out faceCount,
					out vertCount,
					out triCount
					);


				//	Create mesh entity if any faces are exposed
				if(faceCount != 0)
					SetMeshComponent(
						GetMesh(squares[e], faces, blockAccessor[e], heightAccessor[e].ToNativeArray(), adjacentLowestBlocks, faceCount, vertCount, triCount),
						position,
						entity,
						commandBuffer
						);

				commandBuffer.RemoveComponent(entity, typeof(Tags.DrawMesh));
				faces.Dispose();
				adjacentLowestBlocks.Dispose();
			}
		}
		commandBuffer.Playback(entityManager);
		commandBuffer.Dispose();

		chunks.Dispose();
	}	

	//	Generate structs with int values showing face exposure for each block
	public NativeArray<Faces> CheckBlockFaces(MapSquare mapSquare, AdjacentSquares adjacentSquares, NativeArray<int> adjacentLowestBlocks, DynamicBuffer<Block>[] adjacentBlocks, DynamicBuffer<Block> blocks, out int faceCount, out int vertCount, out int triCount)
	{
		int cubeSlice = cubeSize * cubeSize;
		var exposedFaces = new NativeArray<Faces>(blocks.Length, Allocator.TempJob);

		//	TODO get arrays from entities here using GetBuffer().ToNativeArray()
		NativeArray<Block>[] adjacent = new NativeArray<Block>[] {
			new NativeArray<Block>(adjacentBlocks[0].Length, Allocator.TempJob),
			new NativeArray<Block>(adjacentBlocks[1].Length, Allocator.TempJob),
			new NativeArray<Block>(adjacentBlocks[2].Length, Allocator.TempJob),
			new NativeArray<Block>(adjacentBlocks[3].Length, Allocator.TempJob)
		};

		for(int i = 0; i < 4; i++)
			adjacent[i].CopyFrom(adjacentBlocks[i].ToNativeArray());

		
		var job = new FacesJob(){
			exposedFaces = exposedFaces,
			mapSquare = mapSquare,

			blocks 	= blocks.ToNativeArray(),
			right 	= adjacent[0],
			left 	= adjacent[1],
			front 	= adjacent[2],
			back 	= adjacent[3],

			adjacentLowestBlocks = adjacentLowestBlocks,
			
			cubeSize 	= cubeSize,
			cubeSlice	= cubeSlice,
			util 		= new JobUtil()
			};
		
		job.Schedule(mapSquare.blockGenerationArrayLength - (cubeSlice * 2), batchSize).Complete();
		

		for(int i = 0; i < 4; i++)
			adjacent[i].Dispose();

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
				//	Slopes always need two extra verts
				if(blocks[i].slopeType != SlopeType.NOTSLOPED) vertCount += 2;

				//	Count tris because they have an arbitrary ratio to verts
				triCount += count * 6;

				faceCount += count;
			}
		}

		return exposedFaces;
	}

	//	Generate list of Y offsets for top 4 cube vertices
	void GetSlopes(NativeArray<Topology> heightMap, DynamicBuffer<Topology>[] adjacentHeightMaps, DynamicBuffer<Block> blocks, Position squarePosition)
	{

		//int slopeCount = 0;
		float3[] directions = Util.CardinalDirections();
		for(int h = 0; h < heightMap.Length; h++)
		{
			int height = heightMap[h].height;

			//	This position
			float3 pos = Util.Unflatten2D(h, cubeSize);

			//	DEBUGDEBUGDEBUGE
			int blockIndex = 0;// Util.BlockIndex(new float3(pos.x, height, pos.z), cubeSize);
			Block block = blocks[blockIndex];

			//	Block type is not sloped
			if(BlockTypes.sloped[block.type] == 0) continue;

			//	Height differences for all adjacent positions
			float[] differences = new float[directions.Length];

			int heightDifferenceCount = 0;

			for(int d = 0; d < directions.Length; d++)
			{
				int xPos = (int)(directions[d].x + pos.x);
				int zPos = (int)(directions[d].z + pos.z);

				//	Direction of the adjacent map square that owns the required block
				//	(0,0,0) if required block is in this map square 
				float3 edge = new float3(
					xPos == cubeSize ? 1 : xPos < 0 ? -1 : 0,
					0,
					zPos == cubeSize ? 1 : zPos < 0 ? -1 : 0
					); 

				int adjacentHeight;

				//	Block is outside map square
				if(	edge.x != 0 || edge.z != 0)
					adjacentHeight = adjacentHeightMaps[Util.DirectionToIndex(edge)][Util.WrapAndFlatten2D(xPos, zPos, cubeSize)].height;
				//	Block is inside map square
				else
					adjacentHeight = heightMap[Util.Flatten2D(xPos, zPos, cubeSize)].height;

				//	Height difference in blocks
				int difference = adjacentHeight - height;

				if(difference != 0) heightDifferenceCount++;

				differences[d] = difference;
			}

			//	Terrain is not sloped
			if(heightDifferenceCount == 0) continue;
			
			//	Get vertex offsets (-1 to 1) for top vertices of cube required for slope.
			float frontRight	= GetVertexOffset(differences[0], differences[2], differences[4]);	//	front right
			float backRight		= GetVertexOffset(differences[0], differences[3], differences[6]);	//	back right
			float frontLeft		= GetVertexOffset(differences[1], differences[2], differences[5]);	//	front left
			float backLeft 		= GetVertexOffset(differences[1], differences[3], differences[7]);	//	back left

			int changedVertexCount = 0;

			if(frontRight != 0)	changedVertexCount++;
			if(backRight != 0)	changedVertexCount++;
			if(frontLeft != 0)	changedVertexCount++;
			if(backLeft != 0)	changedVertexCount++;

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

	public Mesh GetMesh(MapSquare mapSquare, NativeArray<Faces> faces, DynamicBuffer<Block> blocks, NativeArray<Topology> heightMap, NativeArray<int> adjacentLowestBlocks, int faceCount, int vertCount, int triCount)
	{
		int cubeSlice = (cubeSize * cubeSize);
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

			mapSquare = mapSquare,

			blocks 		= blocks,
			faces 		= faces,
			heightMap	= heightMap,

			util 		= new JobUtil(),
			cubeSize 	= cubeSize,
			cubeSlice	= cubeSlice,

			baseVerts 	= new CubeVertices(true)
		};

		//	Run job
		job.Schedule(mapSquare.blockGenerationArrayLength - (cubeSlice * 2), batchSize).Complete();

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