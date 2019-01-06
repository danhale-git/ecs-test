using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Entities;
using MyComponents;

//[BurstCompile]
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
	int FaceExposed(float3 position, float3 direction, int blockIndex)
	{
		//	Adjacent position
		int3 pos = (int3)(position + direction);

		//	Outside this cube
		if(pos.x == cubeSize)
		{
			int adjacentIndex = AdjacentBlockIndex(pos, mapSquare.bottomBlockBuffer, 0);
			return BlockTypes.visible[right[adjacentIndex].type] == 0 ? 1 : 0;			
		}
		if(pos.x < 0)
		{
			int adjacentIndex = AdjacentBlockIndex(pos, mapSquare.bottomBlockBuffer, 1);
			return BlockTypes.visible[left[adjacentIndex].type] == 0 ? 1 : 0;	
		}
		if(pos.z == cubeSize)
		{
			int adjacentIndex = AdjacentBlockIndex(pos, mapSquare.bottomBlockBuffer, 2);
			return BlockTypes.visible[front[adjacentIndex].type] == 0 ? 1 : 0;	
		}
		if(pos.z < 0)
		{
			int adjacentIndex = AdjacentBlockIndex(pos, mapSquare.bottomBlockBuffer, 3);
			return BlockTypes.visible[back[adjacentIndex].type] == 0 ? 1 : 0;
		}

		//	Inside this cube
		return blocks[util.Flatten(pos.x, pos.y, pos.z, cubeSize)].type == 0 ? 1 : 0;
	}

	int AdjacentBlockIndex(float3 pos, int lowest, int adjacentSquareIndex)
	{
		return util.WrapAndFlatten(new int3(
				(int)pos.x,
				(int)pos.y + (lowest - adjacentLowestBlocks[adjacentSquareIndex]),
				(int)pos.z
			),
			cubeSize
		);
	}

	public void Execute(int i)
	{
		//	Offset to allow buffer of blocks
		i += mapSquare.drawIndexOffset;

		//	Local position in cube
		float3 positionInMesh = util.Unflatten(i, cubeSize);

		if(blocks[i].type == 0) return;

		int right = 0;
		int left = 0;
		int forward = 0;
		int back = 0;

		//	The other if statement results in unnecessary faces. Needs code for handling slope edge faces
		//if(blocks[blockIndex].backRightSlope >= 0 || blocks[blockIndex].frontRightSlope >= 0)
		//if(blocks[blockIndex].slopeType == 0)
			right  	= FaceExposed(positionInMesh, new float3( 1,	0, 0), i);

		//if(blocks[blockIndex].backLeftSlope >= 0 || blocks[blockIndex].frontLeftSlope >= 0)
		//if(blocks[blockIndex].slopeType == 0)
			left  	= FaceExposed(positionInMesh, new float3(-1,	0, 0), i);	

		//if(blocks[blockIndex].frontRightSlope >= 0 || blocks[blockIndex].frontLeftSlope >= 0)		
		//if(blocks[blockIndex].slopeType == 0)
			forward = FaceExposed(positionInMesh, new float3( 0,	0, 1), i);	

		//if(blocks[blockIndex].backRightSlope >= 0 || blocks[blockIndex].backLeftSlope >= 0)
		//if(blocks[blockIndex].slopeType == 0)
			back  	= FaceExposed(positionInMesh, new float3( 0,	0,-1), i);		

		int up  	= FaceExposed(positionInMesh, new float3( 0,	1, 0), i);		//	up
		int down  	= FaceExposed(positionInMesh, new float3( 0,   -1, 0), i);		//	down	


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
