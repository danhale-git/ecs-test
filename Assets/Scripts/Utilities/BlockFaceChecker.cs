using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using MyComponents;


public struct BlockFaceChecker
{
	public NativeArray<Faces> exposedFaces;

	public MapSquare mapSquare;

	//	Block data for this and adjacent map squares
	public NativeArray<Block> current;
	public NativeArray<Block> rightAdjacent;
	public NativeArray<Block> leftAdjacent;
	public NativeArray<Block> frontAdjacent;
	public NativeArray<Block> backAdjacent;

	public NativeArray<int> adjacentLowestBlocks;

	public int squareWidth;
	public NativeArray<float3> directions;	
	public JobUtil util;

	public void Execute(int i)
	{
		//	Offset to allow buffer of blocks
		i += mapSquare.drawIndexOffset;

		if(current[i].type == 0) return;

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
			if(current[i].isSloped == 0)
			{
				faces[d] = exposed > 0 ? (int)Faces.Exp.FULL : (int)Faces.Exp.HIDDEN;
				continue;
			}
			else
			{
				float2 slopeVerts = current[i].slope.GetSlopeVerts(d);

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

		if		(edge.x > 0) return rightAdjacent[AdjacentBlockIndex(pos, mapSquare.bottomBlockBuffer, 0)];
		else if	(edge.x < 0) return leftAdjacent [AdjacentBlockIndex(pos, mapSquare.bottomBlockBuffer, 1)];
		else if	(edge.z > 0) return frontAdjacent[AdjacentBlockIndex(pos, mapSquare.bottomBlockBuffer, 2)];
		else if	(edge.z < 0) return backAdjacent [AdjacentBlockIndex(pos, mapSquare.bottomBlockBuffer, 3)];
		else		    	return current[util.Flatten(pos.x, pos.y, pos.z, squareWidth)];
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
