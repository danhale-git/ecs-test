using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using MyComponents;

[UpdateAfter(typeof(MapSquareSystem))]
public class CubeSystem : ComponentSystem
{
	EntityManager entityManager;
	int cubeSize;


	ArchetypeChunkEntityType entityType;

	ArchetypeChunkComponentType<MapSquare> mapSquareType;
	//ArchetypeChunkComponentType<CubeCount> cubeCountType;

	ArchetypeChunkBufferType<Block> blocksType;
	ArchetypeChunkBufferType<CubePosition> cubePosType;
	ArchetypeChunkBufferType<Height> heightmapType;

	EntityArchetypeQuery mapSquareQuery;

	protected override void OnCreateManager()
	{
		entityManager = World.Active.GetOrCreateManager<EntityManager>();
		cubeSize = TerrainSettings.cubeSize;

		mapSquareQuery = new EntityArchetypeQuery
		{
			Any 	= Array.Empty<ComponentType>(),
			None 	= Array.Empty<ComponentType>(),
			All  	= new ComponentType[] { typeof(MapSquare), typeof(Tags.GenerateBlocks) }
		};
	}

	protected override void OnUpdate()
	{
		entityType 		= GetArchetypeChunkEntityType();

		mapSquareType	= GetArchetypeChunkComponentType<MapSquare>();
		//cubeCountType 	= GetArchetypeChunkComponentType<CubeCount>();

		blocksType 		= GetArchetypeChunkBufferType<Block>();
		cubePosType 	= GetArchetypeChunkBufferType<CubePosition>();
		heightmapType 	= GetArchetypeChunkBufferType<Height>();

		NativeArray<ArchetypeChunk> chunks;
		chunks	= entityManager.CreateArchetypeChunkArray(
						mapSquareQuery, Allocator.TempJob
						);

		if(chunks.Length == 0) chunks.Dispose();
		else GenerateCubes(chunks);
			
	}

	void GenerateCubes(NativeArray<ArchetypeChunk> chunks)
	{
		EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);

		for(int c = 0; c < chunks.Length; c++)
		{
			ArchetypeChunk chunk = chunks[c];
			NativeArray<Entity> entities 					= chunk.GetNativeArray(entityType);

			NativeArray<MapSquare> mapSquares 				= chunk.GetNativeArray(mapSquareType);
			//NativeArray<CubeCount> cubeCounts 			= chunk.GetNativeArray(cubeCountType);

			BufferAccessor<Block> blockAccessor 			= chunk.GetBufferAccessor(blocksType);
			BufferAccessor<CubePosition> cubePosAccessor 	= chunk.GetBufferAccessor(cubePosType);
			BufferAccessor<Height> heightmapAccessor 		= chunk.GetBufferAccessor(heightmapType);

			for(int e = 0; e < mapSquares.Length; e++)
			{
				Entity entity = entities[e];
				MapSquare mapSquare = mapSquares[e];
				float2 position = mapSquare.worldPosition;

				DynamicBuffer<Block> blockBuffer 		= blockAccessor[e];

				DynamicBuffer<CubePosition> cubes		= cubePosAccessor[e];
				DynamicBuffer<Height> heightmap			= heightmapAccessor[e];

				int blockArrayLength = (int)math.pow(cubeSize, 3) * cubes.Length;

				blockBuffer.ResizeUninitialized(blockArrayLength);

				NativeArray<Block> blocks 	= GetBlocks(
												1,
												blockArrayLength,
												heightmap.ToNativeArray(),
												cubes.ToNativeArray()
												);

				for(int b = 0; b < blocks.Length; b++)
					blockBuffer [b] = blocks[b];

				commandBuffer.RemoveComponent(entity, typeof(Tags.GenerateBlocks));

				blocks.Dispose();
			}

			commandBuffer.Playback(entityManager);
			commandBuffer.Dispose();

			chunks.Dispose();
		}
	}

	public NativeArray<Block> GetBlocks(int batchSize, int blockArrayLength, NativeArray<Height> heightMap, NativeArray<CubePosition> cubes)
	{
		var blocks = new NativeArray<Block>(blockArrayLength, Allocator.TempJob);

		int singleCubeArrayLength = (int)math.pow(cubeSize, 3);

		for(int i = 0; i < cubes.Length; i++)
		{
			var job = new BlocksJob()
			{
				blocks = blocks,
				cubeStart = i * singleCubeArrayLength,
				cubePosY = cubes[i].y,

				heightMap = heightMap,
				cubeSize = cubeSize,
				util = new JobUtil()
			};
		
        	job.Schedule(singleCubeArrayLength, batchSize).Complete();	
		}

		return blocks;
	}





}
