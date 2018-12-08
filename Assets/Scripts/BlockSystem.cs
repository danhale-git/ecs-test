using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

[UpdateAfter(typeof(ChunkSystem))]
public class BlockSystem : ComponentSystem
{
	//Util util;
	EntityManager entityManager;
	ArchetypeChunkEntityType entityType;

	ComponentType chunkType;
	ComponentType blockType;
	ComponentType meshType;

	ArchetypeChunkComponentType<Chunk> ChunkType;
	EntityArchetypeQuery newChunkQuery;

	int chunkSize;

	float3 worldStartPosition;

	protected override void OnCreateManager ()
	{
		UnityEngine.Debug.Log("oncreate ran");
		entityManager = World.Active.GetOrCreateManager<EntityManager> ();

		chunkSize = ChunkSystem.chunkSize;

		newChunkQuery = new EntityArchetypeQuery
		{
			Any = Array.Empty<ComponentType> (),
			None = new ComponentType [] { typeof(BLOCKS) },
			All = new ComponentType [] { typeof(Chunk), typeof(CREATE) }
		};
	}
	
	protected override void OnUpdate ()
	{
		entityType = GetArchetypeChunkEntityType ();
		ChunkType = GetArchetypeChunkComponentType<Chunk> ();

		NativeArray<ArchetypeChunk> dataChunks = entityManager.CreateArchetypeChunkArray (newChunkQuery, Allocator.TempJob);
		if (dataChunks.Length == 0)
		{
			dataChunks.Dispose ();
		}
		else
		{
			ProcessChunks (dataChunks);
		}
	}

	void ProcessChunks(NativeArray<ArchetypeChunk> dataChunks)
	{
		EntityCommandBuffer eCBuffer = new EntityCommandBuffer (Allocator.Temp);

		for (int d = 0; d < dataChunks.Length; d++)
		{
			var dataChunk = dataChunks [d];
			var entities = dataChunk.GetNativeArray (entityType);
			var chunks = dataChunk.GetNativeArray (ChunkType);

			for (int e = 0; e < entities.Length; e++)
			{
				var ourChunkEntity = entities [e];
				var ourChunk = chunks [e];
				var chunkWorldPosition = ourChunk.worldPosition;

				DynamicBuffer<Block> blockBuffer = entityManager.GetBuffer<Block> (ourChunkEntity);

				int requiredBlockArraySize = chunkSize * chunkSize * chunkSize;
				if (blockBuffer.Length < requiredBlockArraySize)
				{
					blockBuffer.ResizeUninitialized (requiredBlockArraySize);
				}

				//	get height map
				NativeArray<float> noiseMap = GetSimplexMatrix(chunkWorldPosition, chunkSize, 5678, 0.05f);

				NativeArray<int> heightMap = new NativeArray<int>(noiseMap.Length, Allocator.Temp);

				for(int i = 0; i < noiseMap.Length; i++)
				    heightMap[i] = (int)(noiseMap[i] * chunkSize);

				noiseMap.Dispose();

				NativeArray<Block> blocks = GetBlocks(16, heightMap);

				heightMap.Dispose();

				for (int b = 0; b < blocks.Length; b++)
					blockBuffer [b] = blocks[b];

				eCBuffer.AddComponent (ourChunkEntity, new BLOCKS ());
				
				blocks.Dispose ();
			}
			eCBuffer.Playback (entityManager);
			eCBuffer.Dispose ();

			dataChunks.Dispose ();
		}
	}


	public NativeArray<float> GetSimplexMatrix(float3 chunkPosition, int chunkSize, int seed, float frequency)
    {
        int arrayLength = (int)math.pow(chunkSize, 2);

        //  Native and normal array
        var heightMap = new NativeArray<float>(arrayLength, Allocator.TempJob);

        var job = new FastNoiseJob()
        {
            heightMap = heightMap,
			offset = chunkPosition,
			chunkSize = chunkSize,
            seed = seed,
            frequency = frequency,
			util = new JobUtil(),
            noise = new SimplexNoiseGenerator(0)
        };

        //  Fill native array
        JobHandle jobHandle = job.Schedule(arrayLength, 16);
        jobHandle.Complete();

        //  Copy to normal array and return
        job.noise.Dispose();

		return heightMap;
    }


	public NativeArray<Block> GetBlocks(int batchSize, NativeArray<int> heightMap)
	{
		int blockArrayLength = (int)math.pow(chunkSize, 3);

		//	Native and normal array
		var blocks = new NativeArray<Block>(blockArrayLength, Allocator.TempJob);

		var job = new GenerateBlocksJob()
		{
			blocks = blocks,
			heightMap = heightMap,
			chunkSize = chunkSize,
			util = new JobUtil()
		};
		
		//  Fill native array
        JobHandle jobHandle = job.Schedule(blockArrayLength, batchSize);
        jobHandle.Complete();

		return blocks;
	}
} 