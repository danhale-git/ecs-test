using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Entities;

[BurstCompile]
struct GenerateBlocksJob : IJobParallelFor
{
	public NativeArray<Block> blocks;

	[ReadOnly] public NativeArray<int> heightMap;
	[ReadOnly] public int chunkSize;
	[ReadOnly] public JobUtil util;

	public void Execute(int i)
	{
		//	Get local position in heightmap
		float3 pos = util.Unflatten(i, chunkSize);
		int hMapIndex = util.Flatten2D((int)pos.x, (int)pos.z, chunkSize);
		int type = 0;

		if(pos.y < heightMap[hMapIndex])
			type = 1;

		blocks[i] = new Block
		{
				blockIndex = i, 
				blockType = type,
				localPosition = pos,
		};
	}
}