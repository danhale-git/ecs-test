using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Entities;

[BurstCompile]
struct CheckBlockFacesJob : IJobParallelFor
{
	public NativeArray<Faces> exposedFaces;

	[ReadOnly] public NativeArray<int> right;
	[ReadOnly] public NativeArray<int> left;
	[ReadOnly] public NativeArray<int> up;
	[ReadOnly] public NativeArray<int> down;
	[ReadOnly] public NativeArray<int> forward;
	[ReadOnly] public NativeArray<int> back;

	[ReadOnly] public DynamicBuffer<Block> blocks;
	[ReadOnly] public int chunkSize;
	[ReadOnly] public JobUtil util;

	int FaceExposed(float3 position, float3 direction)
	{
		int3 pos = (int3)(position + direction);

		if(pos.x == chunkSize) 	return right[util.WrapAndFlatten(pos, chunkSize)]   == 0 ? 1 : 0;
		if(pos.x < 0)			return left[util.WrapAndFlatten(pos, chunkSize)] 	== 0 ? 1 : 0;
		if(pos.y == chunkSize) 	return up[util.WrapAndFlatten(pos, chunkSize)] 	    == 0 ? 1 : 0;
		if(pos.y < 0)			return down[util.WrapAndFlatten(pos, chunkSize)]	== 0 ? 1 : 0;
		if(pos.z == chunkSize) 	return forward[util.WrapAndFlatten(pos, chunkSize)] == 0 ? 1 : 0;
		if(pos.z < 0)			return back[util.WrapAndFlatten(pos, chunkSize)] 	== 0 ? 1 : 0;

		return blocks[util.Flatten(pos, chunkSize)].blockType == 0 ? 1 : 0;
	}

	public void Execute(int i)
	{
		//	Get local position in heightmap
		float3 pos = util.Unflatten(i, chunkSize);

		//	Air blocks can't be exposed
		//	TODO is this right? Maybe prevent drawing air block in mesh code instead
		if(blocks[i].blockType == 0) return;

		int right, left, up, down, forward, back;

		right =	FaceExposed(pos, new float3( 1,	0, 0));
		left = 	FaceExposed(pos, new float3(-1,	0, 0));
		up =   	FaceExposed(pos, new float3( 0,	1, 0));
		down = 	FaceExposed(pos, new float3( 0,-1, 0));
		forward=FaceExposed(pos, new float3( 0,	0, 1));
		back = 	FaceExposed(pos, new float3( 0,	0,-1));

		exposedFaces[i] = new Faces(right, left, up, down, forward, back, 0);
	}
}
