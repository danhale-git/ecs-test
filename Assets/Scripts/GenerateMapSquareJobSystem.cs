using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Entities;

class GenerateMapSquareJobSystem
{
	public int[] GetBlocks(int batchSize, int[] _heightMap)
	{
		int chunkSize = MapManager.chunkSize;
		int blockArrayLength = (int)math.pow(chunkSize, 3);

		//	Native and normal array
		var blocks = new NativeArray<int>(blockArrayLength, Allocator.TempJob);
		int[] blockArray = new int[blockArrayLength];

		//	Heightmap
		var heightMap = new NativeArray<int>(_heightMap.Length, Allocator.TempJob);
		heightMap.CopyFrom(_heightMap);

		var job = new GenerateMapSquareJob()
		{
			blocks = blocks,
			heightMap = heightMap,
			chunkSize = chunkSize,
			util = new JobUtil()
		};
		
		//  Fill native array
        JobHandle jobHandle = job.Schedule(blockArrayLength, batchSize);
        jobHandle.Complete();

		//	Copy to normal array and return
		blocks.CopyTo(blockArray);
		blocks.Dispose();
		heightMap.Dispose();

		return blockArray;
	}
}
