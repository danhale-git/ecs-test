using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Entities;
using MyComponents;

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

	[ReadOnly] public int squareWidth;
	[ReadOnly] public NativeArray<float3> directions;	
	[ReadOnly] public JobUtil util;

	public void Execute(int i)
	{
		//	Offset to allow buffer of blocks
		i += mapSquare.drawIndexOffset;

		if(blocks[i].type == 0) return;

		//	Local position in cube
		float3 position = util.Unflatten(i, squareWidth);
		
		Faces faces = new Faces();

		//	Top and bottom faces never have slopes
		faces.up 	= BlockTypes.translucent[GetBlock(position + new float3(0,  1, 0), mapSquare).type];
		faces.down 	= BlockTypes.translucent[GetBlock(position + new float3(0, -1, 0), mapSquare).type];

		//	Right, left, front, back faces might be sloped
		for(int d = 0; d < 4; d++)
		{
			Block adjacentBlock = GetBlock(position + directions[d], mapSquare);
			int exposed = BlockTypes.translucent[adjacentBlock.type];

			//	Not a slope
			if(blocks[i].isSloped == 0)
			{
				faces[d] = exposed > 0 ? (int)Faces.Exp.FULL : (int)Faces.Exp.HIDDEN;
				continue;
			}
			else
			{
				float2 slopeVerts = blocks[i].slope.GetSlopeVerts(d);

				//	Base of a slope, face doesn't exist
				if(slopeVerts.x + slopeVerts.y == -2)
					faces[d] = (int)Faces.Exp.HIDDEN;
				//	No slope on this face, normal behaviour
				else if(slopeVerts.x + slopeVerts.y == 0)
					faces[d] = exposed > 0 ? (int)Faces.Exp.FULL : (int)Faces.Exp.HIDDEN;
				// Slope perpendicular to this face, only half a face needed
				else if(slopeVerts.x + slopeVerts.y == -1)
				{
					if(exposed > 0)
						faces[d] = (int)Faces.Exp.HALFOUT;
					else if(adjacentBlock.slope.slopeType == SlopeType.NOTSLOPED)
						faces[d] = (int)Faces.Exp.HALFIN;
				}
			}
		}
	
		faces.SetCount();

		exposedFaces[i] = faces;
	}

	Block GetBlock(float3 pos, MapSquare mapSquare)
	{
		float3 edge = Util.EdgeOverlap(pos, squareWidth);

		if		(edge.x > 0) return right[AdjacentBlockIndex(pos, mapSquare.bottomBlockBuffer, 0)];
		else if	(edge.x < 0) return left [AdjacentBlockIndex(pos, mapSquare.bottomBlockBuffer, 1)];
		else if	(edge.z > 0) return front[AdjacentBlockIndex(pos, mapSquare.bottomBlockBuffer, 2)];
		else if	(edge.z < 0) return back [AdjacentBlockIndex(pos, mapSquare.bottomBlockBuffer, 3)];
		else		    	return blocks[util.Flatten(pos.x, pos.y, pos.z, squareWidth)];
	}

	int AdjacentBlockIndex(float3 pos, int lowest, int adjacentSquareIndex)
	{
		return util.WrapAndFlatten(new int3(
				(int)pos.x,
				(int)pos.y + (lowest - adjacentLowestBlocks[adjacentSquareIndex]),
				(int)pos.z
			),
			squareWidth
		);
	}

}
