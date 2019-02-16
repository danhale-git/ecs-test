using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Rendering;
using MyComponents;

using UnityEngine;
using UnityEditor;

//	Generate 3D mesh from block data
[UpdateAfter(typeof(MapSlopeSystem))]
[UpdateAfter(typeof(MapBufferChangeSystem))]
[UpdateBefore(typeof(EndFrameTransformSystem))]
public class MapMeshSystem : ComponentSystem
{
	//	Parralel job batch size
	int batchSize = 32;

	EntityManager entityManager;

	int squareWidth;

	ComponentGroup meshGroup;

	public static Material material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/ShaderGraphTest.mat");

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

		squareWidth = TerrainSettings.mapSquareWidth;

		EntityArchetypeQuery squareQuery = new EntityArchetypeQuery{
			None  	= new ComponentType[] { typeof(Tags.EdgeBuffer), typeof(Tags.OuterBuffer), typeof(Tags.InnerBuffer) },
			All  	= new ComponentType[] { typeof(MapSquare), typeof(Tags.DrawMesh) }
		};
		meshGroup = GetComponentGroup(squareQuery);
	}

	//	Query for meshes that need drawing
	protected override void OnUpdate()
	{
		EntityCommandBuffer 		commandBuffer 	= new EntityCommandBuffer(Allocator.Temp);
		NativeArray<ArchetypeChunk> chunks 			= meshGroup.CreateArchetypeChunkArray(Allocator.TempJob);

		ArchetypeChunkEntityType 						entityType 		= GetArchetypeChunkEntityType();
		ArchetypeChunkComponentType<MapSquare> 			squareType		= GetArchetypeChunkComponentType<MapSquare>(true);
		ArchetypeChunkComponentType<Position> 			positionType	= GetArchetypeChunkComponentType<Position>(true);
		ArchetypeChunkComponentType<AdjacentSquares>	adjacentType	= GetArchetypeChunkComponentType<AdjacentSquares>(true);
		ArchetypeChunkBufferType<Block> 				blocksType 		= GetArchetypeChunkBufferType<Block>(true);

		for(int c = 0; c < chunks.Length; c++)
		{
			ArchetypeChunk chunk = chunks[c];

			//	Get chunk data
			NativeArray<Entity> 			entities 		= chunk.GetNativeArray(entityType);
			NativeArray<MapSquare>			squares			= chunk.GetNativeArray(squareType);
			NativeArray<Position>			positions		= chunk.GetNativeArray(positionType);
			NativeArray<AdjacentSquares>	adjacentSquares	= chunk.GetNativeArray(adjacentType);
			BufferAccessor<Block> 			blockAccessor 	= chunk.GetBufferAccessor(blocksType);

			//	Iterate over map square entities
			//for(int e = 0; e < entities.Length; e++)
			for(int e = 0; e < 1; e++)
			{
				Entity entity = entities[e];

				//	Check block face exposure and count mesh arrays
				FaceCounts counts;
				NativeArray<Faces> faces = CheckBlockFaces(squares[e], blockAccessor[e], adjacentSquares[e], out counts);

				bool redraw = entityManager.HasComponent<Tags.Redraw>(entity);

				//	If any faces are exposed, generate mesh and update entity Position component
				if(counts.faceCount != 0)
				{
					Mesh mapSquareMesh = GetMesh(squares[e], faces, blockAccessor[e], counts);

					SetMeshComponent(
						redraw,
						mapSquareMesh,
						entity,
						commandBuffer);					
					
					SetPosition(entity, squares[e], positions[e].Value, commandBuffer);
				}

				if(redraw) commandBuffer.RemoveComponent(entity, typeof(Tags.Redraw));

				commandBuffer.RemoveComponent(entity, typeof(Tags.DrawMesh));
				
				faces.Dispose();
			}
		}
		commandBuffer.Playback(entityManager);
		commandBuffer.Dispose();

		chunks.Dispose();
	}

	//	Generate structs with int values showing face exposure for each block
	NativeArray<Faces> CheckBlockFaces(MapSquare mapSquare, DynamicBuffer<Block> blocks, AdjacentSquares adjacentSquares, out FaceCounts counts)
	{
		NativeArray<Faces> exposedFaces = new NativeArray<Faces>(blocks.Length, Allocator.TempJob);

		NativeArray<float3> directions = Util.CardinalDirections(Allocator.TempJob);

		NativeArray<int> adjacentLowestBlocks = adjacentSquares.GetLowestBlocks(Allocator.TempJob);
		
		FacesJob job = new FacesJob(){
			exposedFaces 	= exposedFaces,
			mapSquare 		= mapSquare,

			blocks 	= blocks.AsNativeArray(),
			right 	= entityManager.GetBuffer<Block>(adjacentSquares[0]).AsNativeArray(),
			left 	= entityManager.GetBuffer<Block>(adjacentSquares[1]).AsNativeArray(),
			front 	= entityManager.GetBuffer<Block>(adjacentSquares[2]).AsNativeArray(),
			back 	= entityManager.GetBuffer<Block>(adjacentSquares[3]).AsNativeArray(),

			adjacentLowestBlocks = adjacentLowestBlocks,
			
			squareWidth = squareWidth,
			directions 	= directions, 
			util 		= new JobUtil()
		};
		
		job.Schedule(mapSquare.blockDrawArrayLength, batchSize).Complete();

		directions.Dispose();
		adjacentLowestBlocks.Dispose();

		counts = CountExposedFaces(blocks, exposedFaces);
		return exposedFaces;
	}

	FaceCounts CountExposedFaces(DynamicBuffer<Block> blocks, NativeArray<Faces> exposedFaces)
	{
		//	Count vertices and triangles	
		int faceCount 	= 0;
		int vertCount 	= 0;
		int triCount 	= 0;
		for(int i = 0; i < exposedFaces.Length; i++)
		{
			int count = exposedFaces[i].count;
			if(count > 0)
			{
				Faces blockFaces = exposedFaces[i];

				//	Starting indices in mesh arrays
				blockFaces.faceIndex 	= faceCount;
				blockFaces.vertIndex 	= vertCount;
				blockFaces.triIndex 	= triCount;

				exposedFaces[i] = blockFaces;

				for(int f = 0; f < 6; f++)
				{
					switch((Faces.Exp)blockFaces[f])
					{
						case Faces.Exp.HIDDEN: break;

						case Faces.Exp.FULL:
							vertCount 	+= 4;
							triCount  	+= 6;
							break;

						case Faces.Exp.HALFOUT:
						case Faces.Exp.HALFIN:
							vertCount 	+= 3;
							triCount 	+= 3;
							break;
					}
				} 
				//	Slopes always need two extra verts
				if(blocks[i].isSloped > 0) vertCount += 2;

				faceCount += count;
			}
		}

		return new FaceCounts(faceCount, vertCount, triCount);
	}

	Mesh GetMesh(MapSquare mapSquare, NativeArray<Faces> faces, DynamicBuffer<Block> blocks, FaceCounts counts)
	{
		//	Determine vertex and triangle arrays using face count
		NativeArray<float3> vertices 	= new NativeArray<float3>(counts.vertCount, Allocator.TempJob);
		NativeArray<float3> normals 	= new NativeArray<float3>(counts.vertCount, Allocator.TempJob);
		NativeArray<int> 	triangles 	= new NativeArray<int>	 (counts.triCount, Allocator.TempJob);
		NativeArray<float4> colors 		= new NativeArray<float4>(counts.vertCount, Allocator.TempJob);

		MeshJob job = new MeshJob(){
			vertices 	= vertices,
			normals 	= normals,
			triangles 	= triangles,
			colors 		= colors,

			mapSquare = mapSquare,

			blocks 	= blocks,
			faces 	= faces,

			util 		= new JobUtil(),
			squareWidth = squareWidth,

			baseVerts = new CubeVertices(true)
		};

		//	Run job
		job.Schedule(mapSquare.blockDrawArrayLength, batchSize).Complete();

		//	Convert vertices and colors from float3/float4 to Vector3/Color
		Vector3[] verticesArray = new Vector3[vertices.Length];
		Vector3[] normalsArray 	= new Vector3[vertices.Length];
		Color[] colorsArray 	= new Color[colors.Length];
		for(int i = 0; i < vertices.Length; i++)
		{
			verticesArray[i] 	= vertices[i];
			normalsArray[i] 	= normals[i];
			colorsArray[i] 		= new Color(colors[i].x, colors[i].y, colors[i].z, colors[i].w);
		}

		//	Tri native array to array
		int[] trianglesArray = new int[triangles.Length];
		triangles.CopyTo(trianglesArray);
		
		vertices.Dispose();
		normals.Dispose();
		colors.Dispose();
		triangles.Dispose();

		return MakeMesh(verticesArray, normalsArray, trianglesArray, colorsArray);
	}

	Mesh MakeMesh(Vector3[] vertices, Vector3[] normals, int[] triangles, Color[] colors)
	{
		Mesh mesh 		= new Mesh();
		mesh.vertices 	= vertices;
		mesh.normals 	= normals;
		mesh.colors 	= colors;
		mesh.SetTriangles(triangles, 0);

		mesh.RecalculateNormals();

		return mesh;
	}

	// Apply mesh to MapSquare entity
	void SetMeshComponent(bool redraw, Mesh mesh, Entity entity, EntityCommandBuffer commandBuffer)
	{
		if(redraw) commandBuffer.RemoveComponent<RenderMesh>(entity);

		RenderMesh renderer = new RenderMesh();
		renderer.mesh = mesh;
		renderer.material = material;

		commandBuffer.AddSharedComponent(entity, renderer);
	}

	void SetPosition(Entity entity, MapSquare mapSquare, float3 currentPosition, EntityCommandBuffer commandBuffer)
	{
		Position newPosition = new Position { Value = new float3(currentPosition.x, mapSquare.bottomBlockBuffer, currentPosition.z) };
		commandBuffer.SetComponent<Position>(entity, newPosition);
	}
} 