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
[UpdateBefore(typeof(TransformSystemGroup))]
public class MapMeshSystem : ComponentSystem
{
	//	Parralel job batch size
	int batchSize = 32;

	EntityManager entityManager;

	int squareWidth;

	ComponentGroup meshGroup;

	public static Material material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/ShaderGraphTest.mat");
	//public static Material material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Default.mat");

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
			All  	= new ComponentType[] { typeof(MapSquare), typeof(Tags.DrawMesh), typeof(AdjacentSquares) }
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
		ArchetypeChunkComponentType<Translation> 			positionType	= GetArchetypeChunkComponentType<Translation>(true);
		ArchetypeChunkComponentType<AdjacentSquares>	adjacentType	= GetArchetypeChunkComponentType<AdjacentSquares>(true);
		ArchetypeChunkBufferType<Block> 				blocksType 		= GetArchetypeChunkBufferType<Block>(true);

		for(int c = 0; c < chunks.Length; c++)
		{
			ArchetypeChunk chunk = chunks[c];

			//	Get chunk data
			NativeArray<Entity> 			entities 		= chunk.GetNativeArray(entityType);
			NativeArray<MapSquare>			squares			= chunk.GetNativeArray(squareType);
			NativeArray<Translation>			positions		= chunk.GetNativeArray(positionType);
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


				//	If any faces are exposed, generate mesh and update entity Position component
				if(counts.faceCount != 0)
				{
					GetMesh(entity, squares[e], faces, blockAccessor[e], counts, commandBuffer);

					commandBuffer.AddComponent<Tags.ApplyMesh>(entity, new Tags.ApplyMesh());

					/*SetMeshComponent(
						redraw,
						mapSquareMesh,
						entity,
						commandBuffer);					
					
					SetPosition(entity, squares[e], positions[e].Value, commandBuffer); */
				}


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

	void GetMesh(Entity entity/*DEBUG */, MapSquare mapSquare, NativeArray<Faces> faces, DynamicBuffer<Block> blocks, FaceCounts counts, EntityCommandBuffer commandBuffer)
	{
		//	Determine vertex and triangle arrays using face count
		NativeArray<float3> vertices 	= new NativeArray<float3>(counts.vertCount, Allocator.TempJob);
		NativeArray<float3> normals 	= new NativeArray<float3>(counts.vertCount, Allocator.TempJob);
		NativeArray<int> 	triangles 	= new NativeArray<int>	 (counts.triCount, Allocator.TempJob);
		NativeArray<float4> colors 		= new NativeArray<float4>(counts.vertCount, Allocator.TempJob);

		//	DEBUG
		DynamicBuffer<WorleyNoise> worleyNoise = entityManager.GetBuffer<WorleyNoise>(entity);

		MeshJob job = new MeshJob(){
			vertices 	= vertices,
			normals 	= normals,
			triangles 	= triangles,
			colors 		= colors,

			mapSquare = mapSquare,
			worleyNoise = worleyNoise,

			blocks 	= blocks,
			faces 	= faces,

			util 		= new JobUtil(),
			squareWidth = squareWidth,

			baseVerts = new CubeVertices(true)
		};

		//	Run job
		job.Schedule(mapSquare.blockDrawArrayLength, batchSize).Complete();

		DynamicBuffer<VertBuffer> vertBuffer = commandBuffer.AddBuffer<VertBuffer>(entity);
		vertBuffer.ResizeUninitialized(counts.vertCount);
		DynamicBuffer<NormBuffer> normBuffer = commandBuffer.AddBuffer<NormBuffer>(entity);
		normBuffer.ResizeUninitialized(counts.vertCount);
		DynamicBuffer<ColorBuffer> colorBuffer = commandBuffer.AddBuffer<ColorBuffer>(entity);
		colorBuffer.ResizeUninitialized(counts.vertCount);

		DynamicBuffer<TriBuffer> triBuffer = commandBuffer.AddBuffer<TriBuffer>(entity);
		triBuffer.ResizeUninitialized(counts.triCount);

		for(int i = 0; i < counts.vertCount; i++)
		{
			vertBuffer[i] = new VertBuffer{ vertex = vertices[i] };
			normBuffer[i] = new NormBuffer{ normal = normals[i] };
			colorBuffer[i] = new ColorBuffer{ color = colors[i] };

		}

		for(int i = 0; i < counts.triCount; i++)
		{
			triBuffer[i] = new TriBuffer{ triangle = triangles[i] };
		}

		vertices.Dispose();
		normals.Dispose();
		colors.Dispose();
		triangles.Dispose();
	}
} 