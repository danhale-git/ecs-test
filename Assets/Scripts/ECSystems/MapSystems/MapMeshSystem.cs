using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using MyComponents;

using UnityEngine;
using UnityEditor;

[UpdateAfter(typeof(MapMeshSystem))]
public class MapMeshBufferSystem : EntityCommandBufferSystem { }

[UpdateAfter(typeof(MapSlopeSystem))]
[UpdateAfter(typeof(MapBufferChangeSystem))]
[UpdateBefore(typeof(TransformSystemGroup))]
public class MapMeshSystem : JobComponentSystem
{
	MapMeshBufferSystem commandBufferSystem;

	int squareWidth;

	public static Material material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/ShaderGraphTest.mat");

	protected override void OnCreateManager()
	{
		commandBufferSystem = World.Active.GetOrCreateManager<MapMeshBufferSystem>();
	}

	protected override JobHandle OnUpdate(JobHandle inputDependencies)
	{
		BlockFacesJob blockFacesJob = new BlockFacesJob{
            commandBuffer = commandBufferSystem.CreateCommandBuffer().ToConcurrent(),
            blocksBuffers    = GetBufferFromEntity<Block>(),
			mapSquares = GetComponentDataFromEntity<MapSquare>(),
			directions = Util.CardinalDirections(Allocator.TempJob)
		};

		JobHandle handle = blockFacesJob.Schedule(this, inputDependencies);
		commandBufferSystem.AddJobHandleForProducer(handle);

		return handle;
	}

	[ExcludeComponent(typeof(Tags.EdgeBuffer), typeof(Tags.OuterBuffer), typeof(Tags.InnerBuffer))]
	[RequireComponentTag(typeof(Tags.DrawMesh), typeof(MapSquare))]
	public struct BlockFacesJob : IJobProcessComponentDataWithEntity<AdjacentSquares>
	{
        public EntityCommandBuffer.Concurrent commandBuffer;

        [ReadOnly] public BufferFromEntity<Block> blocksBuffers;
		[ReadOnly] public ComponentDataFromEntity<MapSquare> mapSquares;

		[DeallocateOnJobCompletion]
		[ReadOnly] public NativeArray<float3> directions;

        public void Execute(Entity entity, int jobIndex, ref AdjacentSquares adjacentSquares)
		{
			FaceCounts counts;
			NativeArray<Faces> faces = CheckBlockFaces(entity, blocksBuffers, mapSquares, adjacentSquares, out counts);

			commandBuffer.AddComponent<FaceCounts>(jobIndex, entity, counts);
			DynamicBuffer<Faces> facesBuffer = commandBuffer.AddBuffer<Faces>(jobIndex, entity);
			facesBuffer.CopyFrom(faces);

			commandBuffer.RemoveComponent(jobIndex, entity, typeof(Tags.DrawMesh));
			
			faces.Dispose();
		}

		NativeArray<Faces> CheckBlockFaces(Entity entity, BufferFromEntity<Block> blocksBuffers, ComponentDataFromEntity<MapSquare> mapSquares, AdjacentSquares adjacentSquares, out FaceCounts counts)
		{
			MapSquare mapSquare = mapSquares[entity];
			DynamicBuffer<Block> blocks = blocksBuffers[entity];

			NativeArray<Faces> exposedFaces = new NativeArray<Faces>(blocks.Length, Allocator.Temp);

			NativeArray<int> adjacentLowestBlocks = new NativeArray<int>(8, Allocator.Temp);
			for(int i = 0; i < 8; i++)
			{
				adjacentLowestBlocks[i] = mapSquares[adjacentSquares[i]].bottomBlockBuffer;
			}
			
			BlockFaceChecker faceChecker = new BlockFaceChecker(){
				exposedFaces 	= exposedFaces,
				mapSquare 		= mapSquare,

				blocks 	= blocks.AsNativeArray(),
				right 	= blocksBuffers[adjacentSquares[0]].AsNativeArray(),
				left 	= blocksBuffers[adjacentSquares[1]].AsNativeArray(),
				front 	= blocksBuffers[adjacentSquares[2]].AsNativeArray(),
				back 	= blocksBuffers[adjacentSquares[3]].AsNativeArray(),

				adjacentLowestBlocks = adjacentLowestBlocks,
				
				squareWidth = TerrainSettings.mapSquareWidth,
				directions 	= directions, 
				util 		= new JobUtil()
			};
			
			for(int i = 0; i < mapSquare.blockDrawArrayLength; i++)
			{
				faceChecker.Execute(i);
			}

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

	}/*






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

			//for(int e = 0; e < entities.Length; e++)
			for(int e = 0; e < 1; e++)
			{
				Entity entity = entities[e];

				FaceCounts counts;
				NativeArray<Faces> faces = CheckBlockFaces(squares[e], blockAccessor[e], adjacentSquares[e], out counts);

				commandBuffer.AddComponent<FaceCounts>(entity, counts);
				DynamicBuffer<Faces> facesBuffer = commandBuffer.AddBuffer<Faces>(entity);
				facesBuffer.CopyFrom(faces);

				commandBuffer.RemoveComponent(entity, typeof(Tags.DrawMesh));
				
				faces.Dispose();
			}
		}
		commandBuffer.Playback(entityManager);
		commandBuffer.Dispose();

		chunks.Dispose();
	}

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
	} */
} 