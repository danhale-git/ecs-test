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
	[ReadOnly] public NativeArray<float3> directions;	
	[ReadOnly] public JobUtil util;

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
		if(blocks[i].type == 0) return;

		//	Local position in cube
		float3 positionInMesh = util.Unflatten(i, cubeSize);

		Faces faces = new Faces();
		faces.up 	= FaceExposed(positionInMesh, new float3( 0,	1, 0), i);
		faces.down 	= FaceExposed(positionInMesh, new float3( 0,   -1, 0), i);

		//	Right, left, front, back
		for(int d = 0; d < 4; d++)
		{
			float2 slopeVerts = blocks[i].GetSlopeVerts(d);
			if(slopeVerts.x >= 0 || slopeVerts.y >= 0)
			{
				faces[d] = FaceExposed(positionInMesh, directions[d], i);
			}
			else
				faces[d] = 0;
		}
	
		faces.SetCount();

		exposedFaces[i] = faces;
	}
}
