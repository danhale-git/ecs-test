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

	[ReadOnly] public MapSquare mapSquare;
	
	[ReadOnly] public DynamicBuffer<Block> blocks;
	[ReadOnly] public NativeArray<Faces> faces;
	[ReadOnly] public NativeArray<Topology> heightMap;

	[ReadOnly] public JobUtil util;
	[ReadOnly] public int cubeSize;
	[ReadOnly] public int cubeSlice;

	[ReadOnly] public CubeVertices baseVerts;

	public void Execute(int i)
	{
		i += mapSquare.drawIndexOffset;
		Block block = blocks[i];

		//	Skip blocks that aren't exposed
		if(faces[i].count == 0) return;

		//	Get block position for vertex offset
		float3 positionInMesh = util.Unflatten(i, cubeSize);

		//	Current local indices
		int vertIndex = 0;
		int triIndex = 0;

		//	Block starting indices
		int vertOffset = faces[i].vertIndex;
		int triOffset = faces[i].triIndex;

		//	Vertices and Triangles for exposed sides

		int faceColor = 0;
		
		for(int f = 0; f < 6; f++)
		{
			int exposure = faces[i][f];
			if(exposure == 0) continue;

			if(f == 4 && block.slopeType != SlopeType.NOTSLOPED)
			{
				DrawSlope(triIndex+triOffset, vertIndex+vertOffset, block, positionInMesh, ref triIndex, ref vertIndex);
				continue;
			}

			switch((Faces.Exp)exposure)
			{
				case Faces.Exp.FULL:
					DrawFace(f, triIndex+triOffset, vertIndex+vertOffset, block, positionInMesh, ref triIndex, ref vertIndex);
					break;
				case Faces.Exp.HALFOUT:
					faceColor = 1;
					DrawHalfFace(f, triIndex+triOffset, vertIndex+vertOffset, block, positionInMesh, (Faces.Exp)faces[i][f], ref triIndex, ref vertIndex);
					break;
				case Faces.Exp.HALFIN:
					faceColor = 2;
					DrawHalfFace(f, triIndex+triOffset, vertIndex+vertOffset, block, positionInMesh, (Faces.Exp)faces[i][f], ref triIndex, ref vertIndex);
					break;
				default: continue;
			}
		}

		if(block.worldPosition.x == 145 && block.worldPosition.y == 39 && block.worldPosition.z == 640)
		{
			Debug.Log("mesh: "+faces[i][1]);
			for(int v = 0; v < vertIndex; v++)
			{
				colors[v+vertOffset] = new float4(0, 1, 1, 1);
			}
		}
		else
		{	
		//	Vertex colours
			for(int v = 0; v < vertIndex; v++)
			{
				switch(faceColor)
				{
					case 0: colors[v+vertOffset] = BlockTypes.color[blocks[i].type]; break;
					case 1: colors[v+vertOffset] = new float4(0, 1, 0, 1); break;
					case 2: colors[v+vertOffset] = new float4(1, 0, 0, 1); break;
				}
			}

			/*for(int v = 0; v < vertIndex; v++)
			{
				switch((int)block.slopeType)
				{
					case 0: colors[v+vertOffset] = new float4(1, 0, 0, 1); break;
					case 1: colors[v+vertOffset] = new float4(0, 1, 0, 1); break;
					default: colors[v+vertOffset] = BlockTypes.color[blocks[i].type]; break;
				}
			} */
		}

		/*for(int v = 0; v < vertIndex; v++)
		{
			colors[v+vertOffset] = BlockTypes.color[blocks[i].type];
		} */
	}

	void DrawFace(int face, int triOffset, int vertOffset, Block block, float3 position, ref int triIndex, ref int vertIndex)
	{
		Triangles(triOffset, vertOffset);
		Vertices(face, position, block, vertOffset);
		triIndex += 6;
		vertIndex +=  4;
	}

	void DrawHalfFace(int face, int triOffset, int vertOffset, Block block, float3 position, Faces.Exp exposure, ref int triIndex, ref int vertIndex)
	{
		HalfVertices(vertOffset, face, position, block, exposure);
		HalfTriangles(triOffset, vertOffset, face,  exposure);
		vertIndex 	+= 3;
		triIndex 	+= 3;
	}

	void DrawSlope(int triOffset, int vertOffset, Block block, float3 position, ref int triIndex, ref int vertIndex)
	{
		SlopedTriangles(triOffset, vertOffset, block);
		triIndex += 6;
		SlopedVertices(vertOffset, position, block);
		vertIndex += 6;
	}

	//	Vertices for given side
	void Vertices(int side, float3 position, Block block, int index)
	{	
		switch(side)
		{
			case 0:	//	Right
				vertices[index+0] = baseVerts[5]+position;
				vertices[index+1] = baseVerts[6]+position;
				vertices[index+2] = baseVerts[2]+position;
				vertices[index+3] = baseVerts[1]+position;
				break;
			case 1:	//	Left
				vertices[index+0] = baseVerts[7]+position;
				vertices[index+1] = baseVerts[4]+position;
				vertices[index+2] = baseVerts[0]+position;
				vertices[index+3] = baseVerts[3]+position;
				break;
			case 2:	//	Front
				vertices[index+0] = baseVerts[4]+position;
				vertices[index+1] = baseVerts[5]+position;
				vertices[index+2] = baseVerts[1]+position;
				vertices[index+3] = baseVerts[0]+position;
				break;
			case 3:	//	Back
				vertices[index+0] = baseVerts[6]+position;
				vertices[index+1] = baseVerts[7]+position;
				vertices[index+2] = baseVerts[3]+position;
				vertices[index+3] = baseVerts[2]+position;
				break;
			case 4:	//	Top
				vertices[index+0] = baseVerts[7]+position;
				vertices[index+1] = baseVerts[6]+position;
				vertices[index+2] = baseVerts[5]+position;
				vertices[index+3] = baseVerts[4]+position;
				break;
			case 5:	//	Bottom
				vertices[index+0] = baseVerts[0]+position;
				vertices[index+1] = baseVerts[1]+position;
				vertices[index+2] = baseVerts[2]+position;
				vertices[index+3] = baseVerts[3]+position;
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

	void HalfVertices(int index, int side, float3 position, Block block, Faces.Exp exposure)
	{
		float2 slope = block.GetSlopeVerts(side);
		FaceVertices face = baseVerts.FaceVertices(side);

		float3 thirdVertex = float3.zero;

		if(slope.x < 0) thirdVertex = exposure == Faces.Exp.HALFOUT ? face[1] : face[0];
		else if(slope.y < 0) thirdVertex = exposure == Faces.Exp.HALFOUT ? face[0] : face[1];;
		
		vertices[index+0] = position + face[2];
		vertices[index+1] = position + face[3];
		vertices[index+2] = position + thirdVertex;
	}
	void HalfTriangles(int index, int vertIndex, int side, Faces.Exp exposure)
	{
		switch(side)
		{
			case 0:
			case 3:
				HalfTris(index, vertIndex);
				break;
			case 2:
			case 1:
				HalfTrisFlipped(index, vertIndex);				
				break;

		}
	}
	void HalfTris(int index, int vertIndex)
	{
		triangles[index+0] = 0 + vertIndex; 
		triangles[index+1] = 1 + vertIndex; 
		triangles[index+2] = 2 + vertIndex;
	}

	void HalfTrisFlipped(int index, int vertIndex)
	{
		triangles[index+0] = 2 + vertIndex; 
		triangles[index+1] = 1 + vertIndex; 
		triangles[index+2] = 0 + vertIndex;
	}
}



public struct FaceVertices
{
	public readonly float3 v0, v1, v2, v3;
	public FaceVertices(float3 v0, float3 v1, float3 v2, float3 v3)
	{
		this.v0 = v0;
		this.v1 = v1;
		this.v2 = v2;
		this.v3 = v3;
	}

	public float3 this[int side]
	{
		get
		{
			switch(side)
			{
				case 0: return v0;
				case 1: return v1;
				case 2: return v2;
				case 3: return v3;
				default: throw new System.ArgumentOutOfRangeException("Index out of range 3: " + side);
			}
		}
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

	public FaceVertices FaceVertices(int side)
	{
		FaceVertices verts;

		switch(side)
		{
			case 0:	//	Right
				verts = new FaceVertices(
					v5 = new float3( 	 0.5f,  0.5f,	 0.5f ),	//	right top front;
					v6 = new float3( 	 0.5f,  0.5f,	-0.5f ),	//	right top back;
					v1 = new float3( 	 0.5f, -0.5f,	 0.5f ),	//	right bottom front;
					v2 = new float3( 	 0.5f, -0.5f,	-0.5f )		//	right bottom back;
				);
				break;

			case 1:	//	Left
				verts = new FaceVertices(
					v4 = new float3( 	-0.5f,  0.5f,	 0.5f ),	//	left top front;
					v7 = new float3( 	-0.5f,  0.5f,	-0.5f ),	//	left top back;
					v0 = new float3( 	-0.5f, -0.5f,	 0.5f ),	//	left bottom front;
					v3 = new float3( 	-0.5f, -0.5f,	-0.5f ) 	//	left bottom back;
				);
				break;

			case 2:	//	Front
				verts = new FaceVertices(
					v5 = new float3( 	 0.5f,  0.5f,	 0.5f ),	//	right top front;
					v4 = new float3( 	-0.5f,  0.5f,	 0.5f ),	//	left top front;
					v1 = new float3( 	 0.5f, -0.5f,	 0.5f ),	//	right bottom front;
					v0 = new float3( 	-0.5f, -0.5f,	 0.5f )		//	left bottom front;
				);
				break;

			case 3:	//	Back
				verts = new FaceVertices(
					v6 = new float3( 	 0.5f,  0.5f,	-0.5f ),	//	right top back;
					v7 = new float3( 	-0.5f,  0.5f,	-0.5f ),	//	left top back;
					v2 = new float3( 	 0.5f, -0.5f,	-0.5f ),	//	right bottom back;
					v3 = new float3( 	-0.5f, -0.5f,	-0.5f ) 	//	left bottom back;
				);
				break;
			default: throw new System.ArgumentOutOfRangeException("Index out of range 3: " + side);
		}
		return verts;
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