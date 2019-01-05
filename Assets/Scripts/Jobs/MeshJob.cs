using UnityEngine;

using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Entities;
using MyComponents;

//[BurstCompile]
//DisposeSentinal errors
struct MeshJob : IJobParallelFor
{
	[NativeDisableParallelForRestriction] public NativeArray<float3> vertices;
	[NativeDisableParallelForRestriction] public NativeArray<float3> normals;
	[NativeDisableParallelForRestriction] public NativeArray<int> triangles;
	[NativeDisableParallelForRestriction] public NativeArray<float4> colors;

	[ReadOnly] public MapSquare square;
	
	[ReadOnly] public DynamicBuffer<Block> blocks;
	[ReadOnly] public NativeArray<Faces> faces;
	[ReadOnly] public NativeArray<Topology> heightMap;

	[ReadOnly] public JobUtil util;
	[ReadOnly] public int cubeSize;

	[ReadOnly] public CubeVertices baseVerts;

	public void Execute(int i)
	{
		Block block = blocks[i];

		//	Skip blocks that aren't exposed
		if(faces[i].count == 0) return;

		//	Mesh will have slopes if > 0
		int sloped = BlockTypes.sloped[blocks[i].type];

		//	Get block position for vertex offset
		float3 positionInMesh = util.Unflatten(i, cubeSize);

		//	Current local indices
		int vertIndex = 0;
		int triIndex = 0;

		//	Block starting indices
		int vertOffset = faces[i].vertIndex;
		int triOffset = faces[i].triIndex;

		//	Vertices and Triangles for exposed sides
		if(faces[i].right == 1)
		{
			Triangles(triIndex+triOffset, vertIndex+vertOffset);
			triIndex += 6;
			Vertices(0, positionInMesh, block, vertIndex+vertOffset, sloped);
			vertIndex +=  4;
		}
		if(faces[i].left == 1)
		{
			Triangles(triIndex+triOffset, vertIndex+vertOffset);
			triIndex += 6;
			Vertices(1, positionInMesh, block, vertIndex+vertOffset, sloped);
			vertIndex +=  4;
		}
		if(faces[i].up == 1)
		{
			if(block.slopeType != SlopeType.NOTSLOPED)
			{
				SlopedTriangles(triIndex+triOffset, vertIndex+vertOffset, block);
				triIndex += 6;
				SlopedVertices(vertIndex+vertOffset, positionInMesh, block);
				vertIndex += 6;
			}
			else
			{
				Triangles(triIndex+triOffset, vertIndex+vertOffset);
				triIndex += 6;
				Vertices(2, positionInMesh, block, vertIndex+vertOffset, sloped);
				vertIndex +=  4;
			}
		}
		if(faces[i].down == 1)
		{
			Triangles(triIndex+triOffset, vertIndex+vertOffset);
			triIndex += 6;
			Vertices(3, positionInMesh, block, vertIndex+vertOffset, sloped);
			vertIndex +=  4;
		}
		if(faces[i].forward == 1)
		{
			Triangles(triIndex+triOffset, vertIndex+vertOffset);
			triIndex += 6;
			Vertices(4, positionInMesh, block, vertIndex+vertOffset, sloped);
			vertIndex +=  4;
		}
		if(faces[i].back == 1)
		{
			Triangles(triIndex+triOffset, vertIndex+vertOffset);
			triIndex += 6;
			Vertices(5, positionInMesh, block, vertIndex+vertOffset, sloped);
			vertIndex +=  4;
		}

		//	Vertex colours
		for(int v = 0; v < vertIndex; v++)
		{
			colors[v+vertOffset] = BlockTypes.color[blocks[i].type];
		}	 
	}

	//	Vertices for given side
	void Vertices(int side, float3 position, Block block, int index, int sloped)
	{
		float3 frontRight;
		float3 backRight;
		float3 frontLeft;
		float3 backLeft;

		//	Terrain is a slope and block type is sloped
		if(block.slopeType != SlopeType.NOTSLOPED && sloped > 0)
		{
			frontRight 	= new float3(0, block.frontRightSlope, 0);
			backRight 	= new float3(0, block.backRightSlope, 0);
			frontLeft 	= new float3(0, block.frontLeftSlope, 0);
			backLeft 	= new float3(0, block.backLeftSlope, 0);
		}
		else
		{
			frontRight = float3.zero;
			backRight = float3.zero;
			backLeft = float3.zero;
			frontLeft = float3.zero;
		}
		
		switch(side)
		{
			case 0:	//	Right
				vertices[index+0] = baseVerts[5]+position;//+frontRight+position;
				vertices[index+1] = baseVerts[6]+position;//+backRight+position;
				vertices[index+2] = baseVerts[2]+position;
				vertices[index+3] = baseVerts[1]+position;
				break;
			case 1:	//	Left
				vertices[index+0] = baseVerts[7]+position;//+backLeft+position;
				vertices[index+1] = baseVerts[4]+position;//+frontLeft+position;
				vertices[index+2] = baseVerts[0]+position;
				vertices[index+3] = baseVerts[3]+position;
				break;
			case 2:	//	Top
				vertices[index+0] = baseVerts[7]+position;//+backLeft+position;
				vertices[index+1] = baseVerts[6]+position;//+backRight+position;
				vertices[index+2] = baseVerts[5]+position;//+frontRight+position;
				vertices[index+3] = baseVerts[4]+position;//+frontLeft+position;
				break;
			case 3:	//	Bottom
				vertices[index+0] = baseVerts[0]+position;
				vertices[index+1] = baseVerts[1]+position;
				vertices[index+2] = baseVerts[2]+position;
				vertices[index+3] = baseVerts[3]+position;
				break;
			case 4:	//	Front
				vertices[index+0] = baseVerts[4]+position;//+frontLeft+position;
				vertices[index+1] = baseVerts[5]+position;//+frontRight+position;
				vertices[index+2] = baseVerts[1]+position;
				vertices[index+3] = baseVerts[0]+position;
				break;
			case 5:	//	Back
				vertices[index+0] = baseVerts[6]+position;//+backRight+position;
				vertices[index+1] = baseVerts[7]+position;//+backLeft+position;
				vertices[index+2] = baseVerts[3]+position;
				vertices[index+3] = baseVerts[2]+position;
				break;
			default: throw new System.ArgumentOutOfRangeException("Index out of range 5: " + side);
		}
	}

	void Triangles(int index, int vertIndex)
	{
		triangles[index+0] = 3 + vertIndex; 
		triangles[index+1] = 1 + vertIndex; 
		triangles[index+2] = 0 + vertIndex; 
		triangles[index+3] = 3 + vertIndex; 
		triangles[index+4] = 2 + vertIndex; 
		triangles[index+5] = 1 + vertIndex;
	}

	void GetSlopeVerts(int index, float3 position, Block block)
	{
		vertices[index+0] = baseVerts[7]+new float3(0, block.backLeftSlope, 0)+position;
		vertices[index+1] = baseVerts[6]+new float3(0, block.backRightSlope, 0)+position;
		vertices[index+2] = baseVerts[5]+new float3(0, block.frontRightSlope, 0)+position;
		vertices[index+3] = baseVerts[4]+new float3(0, block.frontLeftSlope, 0)+position;
		vertices[index+4] = baseVerts[4]+new float3(0, block.frontLeftSlope, 0)+position;
		vertices[index+5] = baseVerts[4]+new float3(0, block.frontLeftSlope, 0)+position;

	}

	//	Triangles are always the same set offset to vertex index
	//	and align so rect division always bisects slope direction
	void SlopedTriangles(int index, int vertIndex, Block block)
	{
		//	Slope is facing NW or SE
		if(block.slopeFacing == SlopeFacing.NWSE)
			TrianglesNWSE(index, vertIndex);
		//	Slope is facing NE or SW
		else
			TrianglesSWNE(index, vertIndex);
	}
	void SlopedVertices(int index, float3 position, Block block)
	{
		//	Slope is facing NW or SE
		if(block.slopeFacing == SlopeFacing.NWSE)
			SlopeVertsNWSE(index, position, block);
		//	Slope is facing NE or SW
		else
			SlopeVertsSWNE(index, position, block);
	}
	
	void SlopeVertsSWNE(int index, float3 position, Block block)
	{
		vertices[index+0] = baseVerts[7]+new float3(0, block.backLeftSlope, 0)+position;	//	back Left
		vertices[index+1] = baseVerts[6]+new float3(0, block.backRightSlope, 0)+position;	//	back Right
		vertices[index+2] = vertices[index+1];
		vertices[index+3] = baseVerts[5]+new float3(0, block.frontRightSlope, 0)+position;	//	front Right
		vertices[index+4] = baseVerts[4]+new float3(0, block.frontLeftSlope, 0)+position;	//	front Left
		vertices[index+5] = vertices[index+4];
	}

	void SlopeVertsNWSE(int index, float3 position, Block block)
	{
		vertices[index+0] = baseVerts[7]+new float3(0, block.backLeftSlope, 0)+position;	//	back Left
		vertices[index+1] = vertices[index+0];
		vertices[index+2] = baseVerts[6]+new float3(0, block.backRightSlope, 0)+position;	//	back Right
		vertices[index+3] = baseVerts[5]+new float3(0, block.frontRightSlope, 0)+position;	//	front Right
		vertices[index+4] = vertices[index+3];
		vertices[index+5] = baseVerts[4]+new float3(0, block.frontLeftSlope, 0)+position;	//	front Left
	}

	void TrianglesSWNE(int index, int vertIndex)
	{
		triangles[index+0] = 4 + vertIndex; 
		triangles[index+1] = 1 + vertIndex; 
		triangles[index+2] = 0 + vertIndex; 
		triangles[index+3] = 5 + vertIndex; 
		triangles[index+4] = 3 + vertIndex; 
		triangles[index+5] = 2 + vertIndex;
	}
	void TrianglesNWSE(int index, int vertIndex)
	{
		triangles[index+0] = 3 + vertIndex; 
		triangles[index+1] = 0 + vertIndex; 
		triangles[index+2] = 5 + vertIndex; 
		triangles[index+3] = 1 + vertIndex; 
		triangles[index+4] = 4 + vertIndex; 
		triangles[index+5] = 2 + vertIndex;
	}
}

public struct CubeVertices
{
	public float3 v0; 
	public float3 v1; 
	public float3 v2; 
	public float3 v3; 
	public float3 v4; 
	public float3 v5; 
	public float3 v6; 
	public float3 v7; 

	public CubeVertices(bool param)
	{
		v0 = new float3( 	-0.5f, -0.5f,	 0.5f );	//	left bottom front;
		v1 = new float3( 	 0.5f, -0.5f,	 0.5f );	//	right bottom front;
		v2 = new float3( 	 0.5f, -0.5f,	-0.5f );	//	right bottom back;
		v3 = new float3( 	-0.5f, -0.5f,	-0.5f ); 	//	left bottom back;
		v4 = new float3( 	-0.5f,  0.5f,	 0.5f );	//	left top front;
		v5 = new float3( 	 0.5f,  0.5f,	 0.5f );	//	right top front;
		v6 = new float3( 	 0.5f,  0.5f,	-0.5f );	//	right top back;
		v7 = new float3( 	-0.5f,  0.5f,	-0.5f );	//	left top back;
	}


	public float3 this[int vert]
	{
		get
		{
			switch (vert)
			{
				case 0: return v0;
				case 1: return v1;
				case 2: return v2;
				case 3: return v3;
				case 4: return v4;
				case 5: return v5;
				case 6: return v6;
				case 7: return v7;
				default: throw new System.ArgumentOutOfRangeException("Index out of range 7: " + vert);
			}
		}
	}
}