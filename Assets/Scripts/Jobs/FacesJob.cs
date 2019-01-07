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

		return BlockTypes.translucent[GetBlock(pos).type];
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

	Block GetBlock(float3 pos)
	{
		float3 edge = Util.EdgeOverlap(pos, cubeSize);

		if		(edge.x > 0) return right[AdjacentBlockIndex(pos, mapSquare.bottomBlockBuffer, 0)];
		else if	(edge.x < 0) return left [AdjacentBlockIndex(pos, mapSquare.bottomBlockBuffer, 1)];
		else if	(edge.z > 0) return front[AdjacentBlockIndex(pos, mapSquare.bottomBlockBuffer, 2)];
		else if	(edge.z < 0) return back [AdjacentBlockIndex(pos, mapSquare.bottomBlockBuffer, 3)];
		//if(edge.x == 0 && edge.y == 0)
		else		    	return blocks[util.Flatten(pos.x, pos.y, pos.z, cubeSize)];
	}

	public void Execute(int i)
	{
		//	Offset to allow buffer of blocks
		i += mapSquare.drawIndexOffset;

		if(blocks[i].type == 0) return;

		//	Local position in cube
		float3 position = util.Unflatten(i, cubeSize);

		Faces faces = new Faces();
		faces.up 	= FaceExposed(position, new float3( 0,	1, 0), i);
		faces.down 	= FaceExposed(position, new float3( 0,   -1, 0), i);

		int debug = 0;
		if(blocks[i].worldPosition.x == 145 && blocks[i].worldPosition.y == 39 && blocks[i].worldPosition.z == 640)
		{
			debug = 1;
			Debug.Log("debugging block: "+i);
		}

		//	Right, left, front, back
		for(int d = 0; d < 4; d++)
		{
			Block adjacentBlock = GetBlock(position + directions[d]);
			int exposed = BlockTypes.translucent[adjacentBlock.type];

			//	Not a slope
			if(blocks[i].slopeType == 0)
			{
				if(debug>0 && d == 1)Debug.Log("not slope");
				faces[d] = exposed > 0 ? (int)Faces.Exp.FULL : (int)Faces.Exp.HIDDEN;
				continue;
			}
			else
			{
				float2 slopeVerts = blocks[i].GetSlopeVerts(d);

				if(slopeVerts.x + slopeVerts.y == -2)
				{
					if(debug>0 && d == 1)Debug.Log("hidden by slope");
					faces[d] = (int)Faces.Exp.HIDDEN;
				}
				else if(slopeVerts.x + slopeVerts.y == 0)
				{
					if(debug>0 && d == 1)Debug.Log("full side on sloped block");
					faces[d] = exposed > 0 ? (int)Faces.Exp.FULL : (int)Faces.Exp.HIDDEN;
				}
				// Half face
				else if(slopeVerts.x + slopeVerts.y == -1)
				{
					if(debug>0 && d == 1)
					{
						Debug.Log(adjacentBlock.slopeType);
						Debug.Log(adjacentBlock.worldPosition);
						Debug.Log(directions[d]+" "+d+" half face");
					}
					if(exposed > 0)
						faces[d] = (int)Faces.Exp.HALFOUT;
					else if(adjacentBlock.slopeType == SlopeType.NOTSLOPED)
						faces[d] = (int)Faces.Exp.HALFIN;
				}
			}
		}
		if(debug>0) Debug.Log("face check: "+faces[1]);

			

		/*for(int d = 0; d < 4; d++)
		{
			Block adjacentBlock = GetBlock(position + directions[d]);
			int exposed = BlockTypes.translucent[adjacentBlock.type];

			float2 slopeVerts = blocks[i].GetSlopeVerts(d);

			if(slopeVerts.x + slopeVerts.y == -1)
			{
				faces[d] = adjacentBlock.slopeType != SlopeType.NOTSLOPED ? 0 : 2;
			}
			else
				faces[d] = 0;
		} */
	
		faces.SetCount();

		exposedFaces[i] = faces;
	}
}
