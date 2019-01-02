using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Entities;
using MyComponents;

[BurstCompile]
struct BlocksJob : IJobParallelFor
{
	[NativeDisableParallelForRestriction] public NativeArray<Block> blocks;

	[ReadOnly] public int cubeStart;
	[ReadOnly] public int cubePosY;

	[ReadOnly] public NativeArray<MyComponents.Terrain> heightMap;
	[ReadOnly] public int cubeSize;
	[ReadOnly] public JobUtil util;


	public void Execute(int i)
	{
		float3 pos = util.Unflatten(i, cubeSize);
		float3 position = new float3(pos.x, pos.y+cubePosY, pos.z);

		int hMapIndex = util.Flatten2D((int)pos.x, (int)pos.z, cubeSize);
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

		blocks[i + cubeStart] = new Block
		{
			type = type,
			localPosition = position,
		};
	}
}