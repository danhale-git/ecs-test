using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Entities;
using MyComponents;

//[BurstCompile]
struct BlocksJob : IJobParallelFor
{
	[NativeDisableParallelForRestriction] public NativeArray<Block> blocks;

	[ReadOnly] public MapSquare mapSquare;
	[ReadOnly] public NativeArray<Topology> heightMap;
	[ReadOnly] public int cubeSize;
	[ReadOnly] public JobUtil util;


	public void Execute(int i)
	{
		float3 pos = util.Unflatten(i, cubeSize);

		float3 position = pos + new float3(0, mapSquare.bottomBuffer, 0);

		int hMapIndex = util.Flatten2D((int)position.x, (int)position.z, cubeSize);
		int type = 0;

		if(position.y <= heightMap[hMapIndex].height)
		{
			switch(heightMap[hMapIndex].type)
			{
				case TerrainTypes.DIRT:
					type = 1; break;
				case TerrainTypes.GRASS:
					type = 2; break;
				case TerrainTypes.CLIFF:
					type = 3; break;
			}
		}

		blocks[i] = new Block
		{
			type = type,
			localPosition = position,
		};
	}
}