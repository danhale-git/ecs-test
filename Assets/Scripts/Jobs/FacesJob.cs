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
			int adjacentIndex = AdjacentIndex(pos, mapSquare.bottomBlockBuffer, 0, blocks[blockIndex]);
			//if(adjacentIndex < 0) return -1;
			return right[adjacentIndex].type == 0 ? 1 : 0;			
		}
		if(pos.x < 0)
		{
			int adjacentIndex = AdjacentIndex(pos, mapSquare.bottomBlockBuffer, 1, blocks[blockIndex]);
			//if(adjacentIndex < 0) return -1;
			return left[adjacentIndex].type == 0 ? 1 : 0;	
		}
		if(pos.z == cubeSize)
		{
			int adjacentIndex = AdjacentIndex(pos, mapSquare.bottomBlockBuffer, 2, blocks[blockIndex]);
			//if(adjacentIndex < 0) return -1;
			return front[adjacentIndex].type == 0 ? 1 : 0;	
		}
		if(pos.z < 0)
		{
			int adjacentIndex = AdjacentIndex(pos, mapSquare.bottomBlockBuffer, 3, blocks[blockIndex]);
			//if(adjacentIndex < 0) return -1;
			return back[adjacentIndex].type == 0 ? 1 : 0;
		}

		//	Inside this cube
		return blocks[util.Flatten(pos.x, pos.y, pos.z, cubeSize)].type == 0 ? 1 : 0;
	}

	int AdjacentIndex(float3 pos, int lowest, int adjacentIndex, Block block)
	{
		int yDifference = lowest - adjacentLowestBlocks[adjacentIndex];

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
		int debug = 0;

		i += mapSquare.drawIndexOffset;
		//	Local position in cube
		float3 positionInMesh = util.Unflatten(i, cubeSize);

		//	Skip air blocks
		if(i >= blocks.Length){
			debug = 1;
			Debug.Log("BAD INDEX");
		}
		else if(blocks[i].type == 0) return;

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


		if(right < 0 ||
			left < 0 ||
			forward < 0 ||
			back < 0 ||
			up < 0 ||
			down < 0)
			{
				debug = 1;
			} 

		//	Get get exposed block faces
		exposedFaces[i] = new Faces(
			debug,

			debug > 0 ? 1 : right,
			debug > 0 ? 1 : left,
			debug > 0 ? 1 : up,
			debug > 0 ? 1 : down,
			debug > 0 ? 1 : forward,
			debug > 0 ? 1 : back,
			0,														
			0,
			0
			);




	}
}
