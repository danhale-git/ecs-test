using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using MyComponents;

[UpdateAfter(typeof(MapCubeSystem))]
public class BlockSystem : ComponentSystem
{
	EntityManager entityManager;
	int cubeSize;

	ArchetypeChunkEntityType entityType;
	ArchetypeChunkComponentType<MapCube> cubeType;
	EntityArchetypeQuery cubeQuery;

	protected override void OnCreateManager()
	{
		/*entityManager = World.Active.GetOrCreateManager<EntityManager>();
		cubeSize = TerrainSettings.cubeSize;

		//	Chunks without block data
		cubeQuery = new EntityArchetypeQuery
		{
			Any = Array.Empty<ComponentType>(),
			None = Array.Empty<ComponentType>(),
			All = new ComponentType [] { typeof(MapCube), typeof(Tags.GenerateBlocks) }
		};*/
	}
	
	protected override void OnUpdate()
	{
		/*entityType = GetArchetypeChunkEntityType();
		cubeType = GetArchetypeChunkComponentType<MapCube>();

		NativeArray<ArchetypeChunk> dataChunks = entityManager.CreateArchetypeChunkArray(cubeQuery, Allocator.TempJob);

		if(dataChunks.Length == 0)
			dataChunks.Dispose();
		else
			ProcessChunks(dataChunks);*/
	}

	/*void ProcessChunks(NativeArray<ArchetypeChunk> dataChunks)
	{
		EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);

		for(int d = 0; d < dataChunks.Length; d++)
		{
			var dataChunk = dataChunks[d];
			var entities = dataChunk.GetNativeArray(entityType);
			var cubes = dataChunk.GetNativeArray(cubeType);

			for(int e = 0; e < entities.Length; e++)
			{
				var cubeEntity = entities[e];
				var cubeWorldPosition = cubes[e].worldPosition;

				//	Generate height map
				NativeArray<float> noiseMap = GetSimplexMatrix(cubeWorldPosition, cubeSize, 5678, 0.05f);
				NativeArray<int> heightMap = new NativeArray<int>(noiseMap.Length, Allocator.Temp);

				for(int i = 0; i < noiseMap.Length; i++)
				    heightMap[i] = (int)(noiseMap[i] * cubeSize);

				noiseMap.Dispose();

				//	Generate block types
				NativeArray<Block> blocks = GetBlocks(16, heightMap);
				heightMap.Dispose();

				//	Block data for this chunk
				DynamicBuffer<Block> blockBuffer = entityManager.GetBuffer<Block>(cubeEntity);
				blockBuffer.ResizeUninitialized((int)math.pow(cubeSize, 3));

				for(int b = 0; b < blocks.Length; b++)
					blockBuffer [b] = blocks[b];

				commandBuffer.RemoveComponent(cubeEntity, typeof(Tags.GenerateBlocks));
				
				blocks.Dispose();
			}
			commandBuffer.Playback(entityManager);
			commandBuffer.Dispose();

			dataChunks.Dispose();
		}
	}


	public NativeArray<float> GetSimplexMatrix(float3 chunkPosition, int chunkSize, int seed, float frequency)
    {
        int arrayLength = (int)math.pow(chunkSize, 2);

        var heightMap = new NativeArray<float>(arrayLength, Allocator.TempJob);

        var job = new FastNoiseJob()
        {
            noiseMap = heightMap,
			offset = chunkPosition,
			chunkSize = chunkSize,
            seed = seed,
            frequency = frequency,
			util = new JobUtil(),
            noise = new SimplexNoiseGenerator(0)
        };

        JobHandle jobHandle = job.Schedule(arrayLength, 16);
        jobHandle.Complete();

        job.noise.Dispose();

		return heightMap;
    }


	public NativeArray<Block> GetBlocks(int batchSize, NativeArray<int> heightMap)
	{
		int blockArrayLength = (int)math.pow(cubeSize, 3);

		var blocks = new NativeArray<Block>(blockArrayLength, Allocator.TempJob);

		var job = new BlocksJob()
		{
			blocks = blocks,
			//heightMap = heightMap,
			chunkSize = cubeSize,
			util = new JobUtil()
		};
		
        JobHandle jobHandle = job.Schedule(blockArrayLength, batchSize);
        jobHandle.Complete();

		return blocks;
	}*/
} 