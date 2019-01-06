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

//	Generate 3D mesh from block data
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

	struct FaceCounts
	{
		public readonly int faceCount, vertCount, triCount;
		public FaceCounts(int faceCount, int vertCount, int triCount)
		{
			this.faceCount = faceCount;
			this.vertCount = vertCount;
			this.triCount = triCount;
		}
	}

	protected override void OnCreateManager()
	{
		entityManager = World.Active.GetOrCreateManager<EntityManager>();

		cubeSize = TerrainSettings.cubeSize;
		cubeArrayLength = (int)math.pow(cubeSize, 3);

		squareQuery = new EntityArchetypeQuery{
			Any 	= Array.Empty<ComponentType>(),
			None  	= new ComponentType[] { typeof(Tags.InnerBuffer), typeof(Tags.EdgeBuffer), typeof(Tags.OuterBuffer), typeof(Tags.GenerateBlocks) },
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

				NativeArray<int> adjacentOffsets = new NativeArray<int>(8, Allocator.TempJob);
				for(int i = 0; i < 8; i++)
					adjacentOffsets[i] = entityManager.GetComponentData<MapSquare>(adjacentSquares[i]).bottomBlockBuffer;

				//	Adjacent height maps in 8 directions
                DynamicBuffer<Topology>[] adjacentHeightMaps = new DynamicBuffer<Topology>[8];
				for(int i = 0; i < 8; i++)
					adjacentHeightMaps[i] = entityManager.GetBuffer<Topology>(adjacentSquares[i]);

				//	Vertex offsets for 4 top vertices of each block (slopes)
				GetSlopes(
					squares[e],
					blockAccessor[e],
					heightAccessor[e].ToNativeArray(),
					adjacentHeightMaps
				);

				//	Check block face exposure
				FaceCounts counts;
				NativeArray<Faces> faces = CheckBlockFaces(
					squares[e],
					blockAccessor[e],
					adjacentSquares,
					adjacentOffsets,
					out counts
				);

				//	Create mesh entity if any faces are exposed
				if(counts.faceCount != 0)
					SetMeshComponent(
						GetMesh(squares[e], faces, blockAccessor[e], heightAccessor[e].ToNativeArray(), adjacentOffsets, counts),
						position,
						entity,
						commandBuffer
					);

				commandBuffer.RemoveComponent(entity, typeof(Tags.DrawMesh));
				faces.Dispose();
				adjacentOffsets.Dispose();
			}
		}
		commandBuffer.Playback(entityManager);
		commandBuffer.Dispose();

		chunks.Dispose();
	}

	//	Generate list of Y offsets for top 4 cube vertices
	void GetSlopes(MapSquare mapSquare, DynamicBuffer<Block> blocks, NativeArray<Topology> heightMap, DynamicBuffer<Topology>[] adjacentHeightMaps)
	{

		//int slopeCount = 0;
		float3[] directions = Util.CardinalDirections();
		for(int h = 0; h < heightMap.Length; h++)
		{
			int height = heightMap[h].height;

			//	2D position
			float3 pos = Util.Unflatten2D(h, cubeSize);

			//	3D position
			int blockIndex = Util.Flatten(pos.x, height - mapSquare.bottomBlockBuffer, pos.z, cubeSize);
			Block block = blocks[blockIndex];

			//	Block type is not sloped
			if(BlockTypes.sloped[block.type] == 0) continue;

			//	Height differences for all adjacent positions
			float[] differences = new float[directions.Length];

			int heightDifferenceCount = 0;

			for(int d = 0; d < directions.Length; d++)
			{
				int x = (int)(directions[d].x + pos.x);
				int z = (int)(directions[d].z + pos.z);

				//	Direction of the adjacent map square that owns the required block
				float3 edge = Util.EdgeOverlap(new float3(x, 0, z), cubeSize);

				int adjacentHeight;

				//	Block is outside map square
				if(	edge.x != 0 || edge.z != 0)
					adjacentHeight = adjacentHeightMaps[Util.DirectionToIndex(edge)][Util.WrapAndFlatten2D(x, z, cubeSize)].height;
				//	Block is inside map square
				else
					adjacentHeight = heightMap[Util.Flatten2D(x, z, cubeSize)].height;

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
			
			block.frontRightSlope = frontRight;
			block.backRightSlope = backRight;
			block.frontLeftSlope = frontLeft;
			block.backLeftSlope = backLeft;
			block.slopeType = slopeType;
			block.slopeFacing = slopeFacing;

			blocks[blockIndex] = block;
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

	//	Generate structs with int values showing face exposure for each block
	NativeArray<Faces> CheckBlockFaces(MapSquare mapSquare, DynamicBuffer<Block> blocks, AdjacentSquares adjacentSquares, NativeArray<int> adjacentOffsets, out FaceCounts counts)
	{
		var exposedFaces = new NativeArray<Faces>(blocks.Length, Allocator.TempJob);

		NativeArray<float3> directions = new NativeArray<float3>(8, Allocator.TempJob);
		directions.CopyFrom(Util.CardinalDirections());
		
		var job = new FacesJob(){
			exposedFaces = exposedFaces,
			mapSquare = mapSquare,

			blocks 	= blocks.ToNativeArray(),
			right 	= entityManager.GetBuffer<Block>(adjacentSquares[0]).ToNativeArray(),
			left 	= entityManager.GetBuffer<Block>(adjacentSquares[1]).ToNativeArray(),
			front 	= entityManager.GetBuffer<Block>(adjacentSquares[2]).ToNativeArray(),
			back 	= entityManager.GetBuffer<Block>(adjacentSquares[3]).ToNativeArray(),

			adjacentLowestBlocks = adjacentOffsets,
			
			cubeSize 	= cubeSize,
			directions 	= directions, 
			util 		= new JobUtil()
			};
		
		job.Schedule(mapSquare.drawArrayLength, batchSize).Complete();

		directions.Dispose();

		//	Count total exposed faces and set face indices	
		int faceCount = 0;
		int vertCount = 0;
		int triCount = 0;
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

				for(int f = 0; f < 6; f++)
				{
					switch((Faces.Exp)blockFaces[f])
					{
						case Faces.Exp.HIDDEN: break;

						case Faces.Exp.FULL:
							vertCount += 4;
							triCount  += 6;
							break;

						case Faces.Exp.HALF:
							vertCount 	+= 3;
							triCount 	+= 3;
							break;
					}
				} 
				//	Slopes always need two extra verts
				if(blocks[i].slopeType != SlopeType.NOTSLOPED) vertCount += 2;

				faceCount += count;
			}
		}

		counts = new FaceCounts(faceCount, vertCount, triCount);

		return exposedFaces;
	}

	Mesh GetMesh(MapSquare mapSquare, NativeArray<Faces> faces, DynamicBuffer<Block> blocks, NativeArray<Topology> heightMap, NativeArray<int> adjacentLowestBlocks, FaceCounts counts)
	{
		int cubeSlice = (cubeSize * cubeSize);
		if(blocks.Length == 0)
		{
			Debug.Log("Tried to draw empty cube!");
		}
		//	Determine vertex and triangle arrays using face count
		NativeArray<float3> vertices 	= new NativeArray<float3>(counts.vertCount, Allocator.TempJob);
		NativeArray<float3> normals 	= new NativeArray<float3>(counts.vertCount, Allocator.TempJob);
		NativeArray<int> triangles 		= new NativeArray<int>	 (counts.triCount, Allocator.TempJob);
		NativeArray<float4> colors 		= new NativeArray<float4>(counts.vertCount, Allocator.TempJob);

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
		job.Schedule(mapSquare.drawArrayLength, batchSize).Complete();

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