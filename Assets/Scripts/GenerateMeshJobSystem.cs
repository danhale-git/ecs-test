using UnityEngine;

using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;

class GenerateMeshJobSystem
{
	struct VertJob : IJobParallelFor
	{
		[NativeDisableParallelForRestriction] public NativeArray<float3> vertices;
		[NativeDisableParallelForRestriction] public NativeArray<int> triangles;
		
		[ReadOnly] public NativeArray<Faces> faces;

		[ReadOnly] public MeshGenerator meshGenerator;
		[ReadOnly] public JobUtil util;
		[ReadOnly] public int chunkSize;

		//	Vertices for given side
		int GetVerts(int side, float3 position, int index)
		{
			NativeArray<float3> verts = new NativeArray<float3>(4, Allocator.Temp);
			verts.CopyFrom(meshGenerator.Vertices(side, position));

			for(int v = 0; v < 4; v++)
				vertices[index+v] =  verts[v];

			verts.Dispose();
			return 4;
		}

		//	Triangles are always the same set, offset to vertex index
		int GetTris(int index, int vertIndex)
		{
			NativeArray<int> tris = new NativeArray<int>(6, Allocator.Temp);
			tris.CopyFrom(meshGenerator.Triangles(vertIndex));

			for(int t = 0; t < 6; t++)
				triangles[index+t] =  tris[t];

			tris.Dispose();
			return 6;
		}

		public void Execute(int i)
		{
			//	Skip blocks that aren't exposed
			if(faces[i].count == 0) return;

			//	Get block position for vertex offset
			float3 pos = util.Unflatten(i, chunkSize);

			//	Current local indices
			int vertIndex = 0;
			int triIndex = 0;

			//	Block starting indices
			int vertOffset = faces[i].faceIndex * 4;
			int triOffset = faces[i].faceIndex * 6;

			//	Vertices and Triangles for exposed sides
            if(faces[i].right == 1)
			{
				triIndex += GetTris(triIndex+triOffset, vertIndex+vertOffset);
				vertIndex += GetVerts(0, pos, vertIndex+vertOffset);
			}
			if(faces[i].left == 1)
			{
				triIndex += GetTris(triIndex+triOffset, vertIndex+vertOffset);
				vertIndex += GetVerts(1, pos, vertIndex+vertOffset);
			}
			if(faces[i].up == 1)
			{
				triIndex += GetTris(triIndex+triOffset, vertIndex+vertOffset);
				vertIndex += GetVerts(2, pos, vertIndex+vertOffset);
			}
			if(faces[i].down == 1)
			{
				triIndex += GetTris(triIndex+triOffset, vertIndex+vertOffset);
				vertIndex += GetVerts(3, pos, vertIndex+vertOffset);
			}
			if(faces[i].forward == 1)
			{
				triIndex += GetTris(triIndex+triOffset, vertIndex+vertOffset);
				vertIndex += GetVerts(4, pos, vertIndex+vertOffset);
			}
			if(faces[i].back == 1)
			{
				triIndex += GetTris(triIndex+triOffset, vertIndex+vertOffset);
				vertIndex += GetVerts(5, pos, vertIndex+vertOffset);
			}	
		}
	}

	struct MeshGenerator
	{
		//	Cube corners
		float3 v0;
		float3 v2;
		float3 v3;
		float3 v1;
		float3 v4;
		float3 v5;
		float3 v6;
		float3 v7;

		public MeshGenerator(byte constParam)
		{
			v0 = new float3( 	-0.5f, -0.5f,	 0.5f );	//	left bottom front
			v2 = new float3( 	 0.5f, -0.5f,	-0.5f );	//	right bottom back
			v3 = new float3( 	-0.5f, -0.5f,	-0.5f ); 	//	left bottom back
			v1 = new float3( 	 0.5f, -0.5f,	 0.5f );	//	right bottom front
			v4 = new float3( 	-0.5f,  0.5f,	 0.5f );	//	left top front
			v5 = new float3( 	 0.5f,  0.5f,	 0.5f );	//	right top front
			v6 = new float3( 	 0.5f,  0.5f,	-0.5f );	//	right top back
			v7 = new float3( 	-0.5f,  0.5f,	-0.5f );	//	left top back
		}

		public enum FacesEnum { RIGHT, LEFT, UP, DOWN, FORWARD, BACK };

		
		public float3[] Vertices(int faceInt, float3 offset)
		{	
			FacesEnum face = (FacesEnum)faceInt;
			switch(face)
			{
				case FacesEnum.RIGHT: 	return new float3[] {v5+offset, v6+offset, v2+offset, v1+offset};
				case FacesEnum.LEFT: 	return new float3[] {v7+offset, v4+offset, v0+offset, v3+offset};
				case FacesEnum.UP: 		return new float3[] {v7+offset, v6+offset, v5+offset, v4+offset};
				case FacesEnum.DOWN: 	return new float3[] {v0+offset, v1+offset, v2+offset, v3+offset};
				case FacesEnum.FORWARD: return new float3[] {v4+offset, v5+offset, v1+offset, v0+offset};
				case FacesEnum.BACK: 	return new float3[] {v6+offset, v7+offset, v3+offset, v2+offset};
				default: 				return null;
			}		
		}

		public int[] Triangles(int offset)
		{
			return new int[] {3+offset, 1+offset, 0+offset, 3+offset, 2+offset, 1+offset};
		}
	}

	public Mesh GetMesh(Faces[] exposedFaces, int faceCount)
	{
		int chunkSize = MapManager.chunkSize;

		//	Determine vertex and triangle arrays using face count
		NativeArray<float3> vertices = new NativeArray<float3>(faceCount * 4, Allocator.TempJob);
		NativeArray<int> triangles = new NativeArray<int>(faceCount * 6, Allocator.TempJob);

		NativeArray<Faces> faces = new NativeArray<Faces>(exposedFaces.Length, Allocator.TempJob);
		faces.CopyFrom(exposedFaces);

		var job = new VertJob()
		{
			vertices = vertices,
			triangles = triangles,
			faces = faces,

			util = new JobUtil(),
			meshGenerator = new MeshGenerator(0),
			chunkSize = chunkSize
		};

		//	Run job
		JobHandle handle = job.Schedule(faces.Length, 1);
		handle.Complete();

		//	Vert (float3) native array to (Vector3) array
		Vector3[] verticesArray = new Vector3[vertices.Length];
		for(int i = 0; i < vertices.Length; i++)
			verticesArray[i] = vertices[i];

		//	Tri native array to array
		int[] trianglesArray = new int[triangles.Length];
		triangles.CopyTo(trianglesArray);
		

		vertices.Dispose();
		triangles.Dispose();
		faces.Dispose();

		return MakeMesh(verticesArray, trianglesArray);
	}

	Mesh MakeMesh(Vector3[] vertices, int[] triangles)
	{
		Mesh mesh = new Mesh();
		mesh.vertices = vertices;
		mesh.SetTriangles(triangles, 0);
		mesh.RecalculateNormals();
		UnityEditor.MeshUtility.Optimize(mesh);

		return mesh;
	}
}
