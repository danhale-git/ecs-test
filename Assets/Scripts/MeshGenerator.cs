using Unity.Collections;
using Unity.Mathematics;
using Unity.Entities;
using MyComponents;

struct MeshGenerator
{
    public DynamicBuffer<VertBuffer> vertices;
	public DynamicBuffer<NormBuffer> normals;
	public DynamicBuffer<TriBuffer> triangles;
	public DynamicBuffer<ColorBuffer> colors;

	public MapSquare mapSquare;
	
	public DynamicBuffer<Block> blocks;
	public DynamicBuffer<Faces> faces;

	public JobUtil util;
	public int squareWidth;

	public CubeVertices baseVerts;

	public void Execute(int i)
	{
		i += mapSquare.drawIndexOffset;
		Block block = blocks[i];

		//	Skip blocks that aren't exposed
		if(faces[i].count == 0) return;

		//	Get block position for vertex offset
		float3 positionInMesh = util.Unflatten(i, squareWidth);

		//	Current local indices
		int vertIndex = 0;
		int triIndex = 0;

		//	Block starting indices
		int vertOffset = faces[i].vertIndex;
		int triOffset = faces[i].triIndex;

		//	Vertices and Triangles for exposed sides
		for(int f = 0; f < 6; f++)
		{
			int exposure = faces[i][f];
			if(exposure == 0) continue;

			//	Top face and block is sloped
			if(f == 4 && block.isSloped > 0)
			{
				DrawSlope(triIndex+triOffset, vertIndex+vertOffset, block, positionInMesh, ref triIndex, ref vertIndex);
				continue;
			}

			//	All other faces
			switch((Faces.Exp)exposure)
			{
				//	Normal cube face
				case Faces.Exp.FULL:
					DrawFace(f, triIndex+triOffset, vertIndex+vertOffset, block, positionInMesh, ref triIndex, ref vertIndex);
					break;
				//	Half faces for slope edges
				case Faces.Exp.HALFOUT:
				case Faces.Exp.HALFIN:
					DrawHalfFace(f, triIndex+triOffset, vertIndex+vertOffset, block, positionInMesh, (Faces.Exp)faces[i][f], ref triIndex, ref vertIndex);
					break;
				default: continue;
			}
		}

		for(int v = 0; v < vertIndex; v++)
		{
			colors[v+vertOffset] = new ColorBuffer { color = BlockTypes.color[blocks[i].type] };
		}
	}

	//	Normal face
	void DrawFace(int face, int triOffset, int vertOffset, Block block, float3 position, ref int triIndex, ref int vertIndex)
	{
		Triangles(triOffset, vertOffset);
		Vertices(face, position, block, vertOffset);
		triIndex += 6;
		vertIndex +=  4;
	}

	//	Half face for the edge of sloped blocks
	void DrawHalfFace(int face, int triOffset, int vertOffset, Block block, float3 position, Faces.Exp exposure, ref int triIndex, ref int vertIndex)
	{
		HalfVertices(vertOffset, face, position, block.slope, exposure);
		HalfTriangles(triOffset, vertOffset, face,  exposure);
		vertIndex 	+= 3;
		triIndex 	+= 3;
	}

	//	Top face of a sloped block
	void DrawSlope(int triOffset, int vertOffset, Block block, float3 position, ref int triIndex, ref int vertIndex)
	{
		SlopedTriangles(triOffset, vertOffset, block.slope);
		triIndex += 6;
		SlopedVertices(vertOffset, position, block.slope);
		vertIndex += 6;
	}

	//	Vertices for normal cube
	void Vertices(int side, float3 position, Block block, int index)
	{	
		switch(side)
		{
			case 0:	//	Right
				vertices[index+0] = new VertBuffer { vertex = baseVerts[5]+position };
				vertices[index+1] = new VertBuffer { vertex = baseVerts[6]+position };
				vertices[index+2] = new VertBuffer { vertex = baseVerts[2]+position };
				vertices[index+3] = new VertBuffer { vertex = baseVerts[1]+position };
				break;
			case 1:	//	Left
				vertices[index+0] = new VertBuffer { vertex = baseVerts[7]+position };
				vertices[index+1] = new VertBuffer { vertex = baseVerts[4]+position };
				vertices[index+2] = new VertBuffer { vertex = baseVerts[0]+position };
				vertices[index+3] = new VertBuffer { vertex = baseVerts[3]+position };
				break;
			case 2:	//	Front
				vertices[index+0] = new VertBuffer { vertex = baseVerts[4]+position };
				vertices[index+1] = new VertBuffer { vertex = baseVerts[5]+position };
				vertices[index+2] = new VertBuffer { vertex = baseVerts[1]+position };
				vertices[index+3] = new VertBuffer { vertex = baseVerts[0]+position };
				break;
			case 3:	//	Back
				vertices[index+0] = new VertBuffer { vertex = baseVerts[6]+position };
				vertices[index+1] = new VertBuffer { vertex = baseVerts[7]+position };
				vertices[index+2] = new VertBuffer { vertex = baseVerts[3]+position };
				vertices[index+3] = new VertBuffer { vertex = baseVerts[2]+position };
				break;
			case 4:	//	Top
				vertices[index+0] = new VertBuffer { vertex = baseVerts[7]+position };
				vertices[index+1] = new VertBuffer { vertex = baseVerts[6]+position };
				vertices[index+2] = new VertBuffer { vertex = baseVerts[5]+position };
				vertices[index+3] = new VertBuffer { vertex = baseVerts[4]+position };
				break;
			case 5:	//	Bottom
				vertices[index+0] = new VertBuffer { vertex = baseVerts[0]+position };
				vertices[index+1] = new VertBuffer { vertex = baseVerts[1]+position };
				vertices[index+2] = new VertBuffer { vertex = baseVerts[2]+position };
				vertices[index+3] = new VertBuffer { vertex = baseVerts[3]+position };
				break;
			
			default: throw new System.ArgumentOutOfRangeException("Index out of range 5: " + side);
		}
	}

	//	Triangles for normal cube
	void Triangles(int index, int vertIndex)
	{
		triangles[index+0] = new TriBuffer { triangle = 3 + vertIndex }; 
		triangles[index+1] = new TriBuffer { triangle = 1 + vertIndex }; 
		triangles[index+2] = new TriBuffer { triangle = 0 + vertIndex }; 
		triangles[index+3] = new TriBuffer { triangle = 3 + vertIndex }; 
		triangles[index+4] = new TriBuffer { triangle = 2 + vertIndex }; 
		triangles[index+5] = new TriBuffer { triangle = 1 + vertIndex };
	}
	
	//	Vertices for sloped top face
	//	Add two extra verts to enable hard
	//	edges on the mesh
	void SlopedVertices(int index, float3 position, BlockSlope slope)
	{
		switch(slope.slopeFacing)
		{
			case SlopeFacing.NWSE:
				vertices[index+0] = new VertBuffer { vertex = baseVerts[7]+new float3(0, slope.backLeftSlope, 0)+position };	//	back Left
				vertices[index+1] = vertices[index+0];
				vertices[index+2] = new VertBuffer { vertex = baseVerts[6]+new float3(0, slope.backRightSlope, 0)+position };	//	back Right
				vertices[index+3] = new VertBuffer { vertex = baseVerts[5]+new float3(0, slope.frontRightSlope, 0)+position };	//	front Right
				vertices[index+4] = vertices[index+3];
				vertices[index+5] = new VertBuffer { vertex = baseVerts[4]+new float3(0, slope.frontLeftSlope, 0)+position };	//	front Left
				break;

			case SlopeFacing.SWNE:
				vertices[index+0] = new VertBuffer { vertex = baseVerts[7]+new float3(0, slope.backLeftSlope, 0)+position };	//	back Left
				vertices[index+1] = new VertBuffer { vertex = baseVerts[6]+new float3(0, slope.backRightSlope, 0)+position };	//	back Right
				vertices[index+2] = vertices[index+1];
				vertices[index+3] = new VertBuffer { vertex = baseVerts[5]+new float3(0, slope.frontRightSlope, 0)+position };	//	front Right
				vertices[index+4] = new VertBuffer { vertex = baseVerts[4]+new float3(0, slope.frontLeftSlope, 0)+position };	//	front Left
				vertices[index+5] = vertices[index+4];
				break;
		}
	}

	//	Triangles for sloped top face
	//	align so rect division always bisects
	//	slope direction, for hard slope edges
	void SlopedTriangles(int index, int vertIndex, BlockSlope slope)
	{
		switch(slope.slopeFacing)
		{
			case SlopeFacing.NWSE:
				triangles[index+0] = new TriBuffer { triangle = 3 + vertIndex }; 
				triangles[index+1] = new TriBuffer { triangle = 0 + vertIndex }; 
				triangles[index+2] = new TriBuffer { triangle = 5 + vertIndex }; 
				triangles[index+3] = new TriBuffer { triangle = 1 + vertIndex }; 
				triangles[index+4] = new TriBuffer { triangle = 4 + vertIndex }; 
				triangles[index+5] = new TriBuffer { triangle = 2 + vertIndex };
				break;

			case SlopeFacing.SWNE:
				triangles[index+0] = new TriBuffer { triangle = 4 + vertIndex }; 
				triangles[index+1] = new TriBuffer { triangle = 1 + vertIndex }; 
				triangles[index+2] = new TriBuffer { triangle = 0 + vertIndex }; 
				triangles[index+3] = new TriBuffer { triangle = 5 + vertIndex }; 
				triangles[index+4] = new TriBuffer { triangle = 3 + vertIndex }; 
				triangles[index+5] = new TriBuffer { triangle = 2 + vertIndex };
				break;
		}
	}

	//	Vertices for half face, one triangle arranged to fill above/below two slope vertices
	void HalfVertices(int index, int side, float3 position, BlockSlope slope, Faces.Exp exposure)
	{
		float2 slopeVerts = slope.GetSlopeVerts(side);
		FaceVertices face = baseVerts.FaceVertices(side);

		float3 thirdVertex = float3.zero;

		//	Half face below slope
		if(exposure == Faces.Exp.HALFOUT)
		{
			//	Bottom two face vertices
			vertices[index+0] = new VertBuffer { vertex = position + face[3] };
			vertices[index+1] = new VertBuffer { vertex = position + face[2] };

			//	Top left or right face vertex
			if		(slopeVerts.x < 0) vertices[index+2] = new VertBuffer { vertex = position + face[1] };
			else if	(slopeVerts.y < 0) vertices[index+2] = new VertBuffer { vertex = position + face[0] };
		}
		//	Half face above slope
		else if(exposure == Faces.Exp.HALFIN)
		{
			//	Top two face vertices
			vertices[index+0] = new VertBuffer { vertex = position + face[0] };
			vertices[index+1] = new VertBuffer { vertex = position + face[1] };

			//	Bottom left or right face vertex
			if		(slopeVerts.x < 0) vertices[index+2] = new VertBuffer { vertex = position + face[2] };
			else if	(slopeVerts.y < 0) vertices[index+2] = new VertBuffer { vertex = position + face[3] };
		}
	}

	//	Triangles for half face, flipped depending which side of the block
	void HalfTriangles(int index, int vertIndex, int side, Faces.Exp exposure)
	{
		switch(side)
		{
			case 1:
			case 2:
				triangles[index+0] = new TriBuffer { triangle = 0 + vertIndex }; 
				triangles[index+1] = new TriBuffer { triangle = 1 + vertIndex }; 
				triangles[index+2] = new TriBuffer { triangle = 2 + vertIndex };
				break; 

			case 0:
			case 3:
				triangles[index+0] = new TriBuffer { triangle = 2 + vertIndex }; 
				triangles[index+1] = new TriBuffer { triangle = 1 + vertIndex }; 
				triangles[index+2] = new TriBuffer { triangle = 0 + vertIndex };
				break;
		}
	}
}