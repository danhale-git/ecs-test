using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Entities;
using MyComponents;

//[BurstCompile]
struct BlockFacesJob : IJobParallelFor
{
	[NativeDisableParallelForRestriction] public NativeArray<Faces> exposedFaces;

	//	Block data for this and adjacent map squares
	[ReadOnly] public DynamicBuffer<Block> blocks;
	[ReadOnly] public NativeArray<Block> right;
	[ReadOnly] public NativeArray<Block> left;
	[ReadOnly] public NativeArray<Block> forward;
	[ReadOnly] public NativeArray<Block> back;

	//	Indices where cubes in this map square begin
	[ReadOnly] public int cubeStart;
	[ReadOnly] public int aboveStart;
	[ReadOnly] public int belowStart;

	[ReadOnly] public int cubeSize;
	[ReadOnly] public JobUtil util;

	int FaceExposed(float3 position, float3 direction)
	{
		//	Adjacent position
		int3 pos = (int3)(position + direction);

		//	Outside this cube
		if(pos.x == cubeSize)
			return right[util.WrapAndFlatten(pos, cubeSize) + cubeStart].type 		== 0 ? 1 : 0;
		if(pos.x < 0)
			return left[util.WrapAndFlatten(pos, cubeSize) + cubeStart].type 		== 0 ? 1 : 0;
		if(pos.y == cubeSize)
			return blocks[util.WrapAndFlatten(pos, cubeSize) + aboveStart].type 	== 0 ? 1 : 0;
		if(pos.y < 0)
			return blocks[util.WrapAndFlatten(pos, cubeSize) + belowStart].type		== 0 ? 1 : 0;
		if(pos.z == cubeSize)
			return forward[util.WrapAndFlatten(pos, cubeSize) + cubeStart].type 	== 0 ? 1 : 0;
		if(pos.z < 0)
			return back[util.WrapAndFlatten(pos, cubeSize) + cubeStart].type		== 0 ? 1 : 0;

		//	Inside this cube
		return blocks[util.Flatten(pos, cubeSize) + cubeStart].type == 0 ? 1 : 0;
	}

	public void Execute(int i)
	{
		//	Get local position in cube
		float3 positionInCube = util.Unflatten(i, cubeSize);

		//	Skip invisible blocks
		if(blocks[i + cubeStart].type == 0) return;

		//	Get get exposed block faces
		exposedFaces[i+cubeStart] = new Faces(
			FaceExposed(positionInCube, new float3( 1,	0, 0)),		//	right
			FaceExposed(positionInCube, new float3(-1,	0, 0)),		//	left
			FaceExposed(positionInCube, new float3( 0,	1, 0)),		//	up
			FaceExposed(positionInCube, new float3( 0,   -1, 0)),	//	down	
			FaceExposed(positionInCube, new float3( 0,	0, 1)),		//	forward
			FaceExposed(positionInCube, new float3( 0,	0,-1)),		//	back
			0														//	Face index is set later
			);




	}
}
