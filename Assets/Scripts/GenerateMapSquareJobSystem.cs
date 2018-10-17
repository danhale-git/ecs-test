using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

class GenerateMapSquareJobSystem
{
	struct GenerateJob : IJobParallelFor
	{
		public NativeArray<int> blocks;

		[ReadOnly] public NativeArray<int> heightMap;
		[ReadOnly] public int chunkSize;
		[ReadOnly] public JobUtil util;

		public void Execute(int i)
		{
			//	Get local position in heightmap
			float3 pos = util.Unflatten(i, chunkSize, chunkSize, chunkSize);
			int hMapIndex = util.Flatten2D((int)pos.x+1, (int)pos.z+1, chunkSize+2);

			if(pos.y+1 < heightMap[hMapIndex])
				blocks[i] = 1;
		}
	}

	public int[] GetBlocks(int[] _heightMap)
	{
		int chunkSize = ChunkManager.chunkSize;
		int blockArrayLength = (int)math.pow(chunkSize, 3);

		//	Native and normal array
		var blocks = new NativeArray<int>(blockArrayLength, Allocator.TempJob);
		int[] blockArray = new int[blockArrayLength];

		//	Heightmap
		var heightMap = new NativeArray<int>(_heightMap.Length, Allocator.TempJob);
		heightMap.CopyFrom(_heightMap);

		var job = new GenerateJob()
		{
			blocks = blocks,
			heightMap = heightMap,
			chunkSize = chunkSize,
			util = new JobUtil()
		};
		
		//  Fill native array
        JobHandle jobHandle = job.Schedule(blockArrayLength, 64);
        jobHandle.Complete();

		//	Copy to normal array and return
		blocks.CopyTo(blockArray);
		blocks.Dispose();
		heightMap.Dispose();

		return blockArray;
	}
}
