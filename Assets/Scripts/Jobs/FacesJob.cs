using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Entities;
using MyComponents;

[BurstCompile]
struct FacesJob : IJobParallelFor
{
	[NativeDisableParallelForRestriction] public NativeArray<Faces> exposedFaces;

	//	Block data for this and adjacent map squares
	[ReadOnly] public NativeArray<Block> blocks;
	[ReadOnly] public NativeArray<Block> right;
	[ReadOnly] public NativeArray<Block> left;
	[ReadOnly] public NativeArray<Block> front;
	[ReadOnly] public NativeArray<Block> back;

	//	Indices where cubes in this map square begin
	[ReadOnly] public int cubeStart;
	[ReadOnly] public int aboveStart;
	[ReadOnly] public int belowStart;

	[ReadOnly] public int cubeSize;
	[ReadOnly] public JobUtil util;

	//	TODO: use byte instead
	//	Return 1 for exposed or 0 for hidden
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
			return front[util.WrapAndFlatten(pos, cubeSize) + cubeStart].type 		== 0 ? 1 : 0;
		if(pos.z < 0)
			return back[util.WrapAndFlatten(pos, cubeSize) + cubeStart].type		== 0 ? 1 : 0;

		//	Inside this cube
		return blocks[util.Flatten(pos, cubeSize) + cubeStart].type == 0 ? 1 : 0;
	}

	public void Execute(int i)
	{
		//	Get local position in cube
		float3 positionInCube = util.Unflatten(i, cubeSize);
		

		//	Skip air blocks
		if(blocks[i + cubeStart].type == 0) return;

		int right = 0;
		int left = 0;
		int forward = 0;
		int back = 0;

		//if(blocks[i].backRightSlope >= 0 || blocks[i].frontRightSlope >= 0)
		if(blocks[i].slopeType == SlopeType.NOTSLOPED)
			right  	= FaceExposed(positionInCube, new float3( 1,	0, 0));

		//if(blocks[i].backLeftSlope >= 0 || blocks[i].frontLeftSlope >= 0)
		if(blocks[i].slopeType == SlopeType.NOTSLOPED)
			left  	= FaceExposed(positionInCube, new float3(-1,	0, 0));	

		//if(blocks[i].frontRightSlope >= 0 || blocks[i].frontLeftSlope >= 0)		
		if(blocks[i].slopeType == SlopeType.NOTSLOPED)
			forward = FaceExposed(positionInCube, new float3( 0,	0, 1));	

		//if(blocks[i].backRightSlope >= 0 || blocks[i].backLeftSlope >= 0)
		if(blocks[i].slopeType == SlopeType.NOTSLOPED)
			back  	= FaceExposed(positionInCube, new float3( 0,	0,-1));		

		int up  	= FaceExposed(positionInCube, new float3( 0,	1, 0));		//	up
		int down  	= FaceExposed(positionInCube, new float3( 0,   -1, 0));		//	down	


		//	Get get exposed block faces
		exposedFaces[i+cubeStart] = new Faces(
			right,
			left,
			up,
			down	,
			forward,
			back,
			0,														
			0,
			0
			);




	}
}
