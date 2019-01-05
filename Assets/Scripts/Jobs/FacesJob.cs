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

	[ReadOnly] public MapSquare mapSquare;

	//	Block data for this and adjacent map squares
	[ReadOnly] public NativeArray<Block> blocks;
	[ReadOnly] public NativeArray<Block> right;
	[ReadOnly] public NativeArray<Block> left;
	[ReadOnly] public NativeArray<Block> front;
	[ReadOnly] public NativeArray<Block> back;

	[ReadOnly] public NativeArray<int> adjacentLowestBlocks;

	[ReadOnly] public int cubeSize;
	[ReadOnly] public int cubeSlice;	
	[ReadOnly] public JobUtil util;

	//	TODO: use byte instead
	//	Return 1 for exposed or 0 for hidden
	int FaceExposed(float3 position, float3 direction)
	{
		//	Adjacent position
		int3 pos = (int3)(position + direction);

		//	Outside this cube
		if(pos.x == cubeSize)
			return 1;//right[AdjacentIndex(pos, square.lowestVisibleBlock, adjacentLowestBlocks[0])].type == 0 ? 1 : 0;			
		if(pos.x < 0)
			return 1;//left[AdjacentIndex(pos, square.lowestVisibleBlock, adjacentLowestBlocks[1])].type == 0 ? 1 : 0;	
		if(pos.z == cubeSize)
			return 1;//front[AdjacentIndex(pos, square.lowestVisibleBlock, adjacentLowestBlocks[2])].type == 0 ? 1 : 0;	
		if(pos.z < 0)
			return 1;//back[AdjacentIndex(pos, square.lowestVisibleBlock, adjacentLowestBlocks[3])].type == 0 ? 1 : 0;

		//	Inside this cube
		return blocks[util.Flatten(pos.x, pos.y, pos.z, cubeSize)].type == 0 ? 1 : 0;
	}

	int AdjacentIndex(float3 pos, int lowest, int adjacentLowest)
	{
		int yDifference = lowest - adjacentLowest;

		float3 wrapped = util.WrapBlockIndex((int3)pos, cubeSize);

		int adjusted = util.Flatten(new float3(
				wrapped.x,
				wrapped.y + yDifference,
				wrapped.z
			),
			cubeSize
		);

		return adjusted;
	}

	public void Execute(int i)
	{
		i += mapSquare.drawIndexOffset;
		//	Local position in cube
		float3 positionInMesh = util.Unflatten(i, cubeSize);

		//	Block index in map square
		//int blockIndex = i;//util.BlockIndex(positionInCube + new float3(0, cubeHeight, 0), cubeSize);

		//	Skip air blocks
		if(blocks[i].type == 0) return;

		int right = 0;
		int left = 0;
		int forward = 0;
		int back = 0;

		//	The other if statement results in unnecessary faces. Needs code for handling slope edge faces
		//if(blocks[blockIndex].backRightSlope >= 0 || blocks[blockIndex].frontRightSlope >= 0)
		//if(blocks[blockIndex].slopeType == 0)
			right  	= FaceExposed(positionInMesh, new float3( 1,	0, 0));

		//if(blocks[blockIndex].backLeftSlope >= 0 || blocks[blockIndex].frontLeftSlope >= 0)
		//if(blocks[blockIndex].slopeType == 0)
			left  	= FaceExposed(positionInMesh, new float3(-1,	0, 0));	

		//if(blocks[blockIndex].frontRightSlope >= 0 || blocks[blockIndex].frontLeftSlope >= 0)		
		//if(blocks[blockIndex].slopeType == 0)
			forward = FaceExposed(positionInMesh, new float3( 0,	0, 1));	

		//if(blocks[blockIndex].backRightSlope >= 0 || blocks[blockIndex].backLeftSlope >= 0)
		//if(blocks[blockIndex].slopeType == 0)
			back  	= FaceExposed(positionInMesh, new float3( 0,	0,-1));		

		int up  	= FaceExposed(positionInMesh, new float3( 0,	1, 0));		//	up
		int down  	= FaceExposed(positionInMesh, new float3( 0,   -1, 0));		//	down	


		//	Get get exposed block faces
		exposedFaces[i] = new Faces(
			right,
			left,
			up,
			down,
			forward,
			back,
			0,														
			0,
			0
			);




	}
}
