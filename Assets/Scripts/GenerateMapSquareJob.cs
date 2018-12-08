using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Burst;

[BurstCompile]
struct GenerateMapSquareJob : IJobParallelFor
{
	public NativeArray<int> blocks;

	[ReadOnly] public NativeArray<int> heightMap;
	[ReadOnly] public int chunkSize;
	[ReadOnly] public JobUtil util;

	public void Execute(int i)
	{
		//	Get local position in heightmap
		float3 pos = util.Unflatten(i, chunkSize);
		int hMapIndex = util.Flatten2D((int)pos.x, (int)pos.z, chunkSize);

		if(pos.y < heightMap[hMapIndex])
			blocks[i] = 1;
	}
}