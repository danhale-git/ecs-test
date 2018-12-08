using UnityEngine;

using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Burst;

class GenerateMeshJobSystem
{
	//[BurstCompile]
	//DisposeSentinal errors
	struct VertJob : IJobParallelFor
	{
		[NativeDisableParallelForRestriction] public NativeArray<float3> vertices;
		[NativeDisableParallelForRestriction] public NativeArray<int> triangles;
		
		[ReadOnly] public NativeArray<Faces> faces;

		//[ReadOnly] public MeshGenerator meshGenerator;
		[ReadOnly] public JobUtil util;
		[ReadOnly] public int chunkSize;

		[ReadOnly] public Vertices baseVerts;

		//	Vertices for given side
		int GetVerts(int side, float3 position, int index)
		{
			switch(side)
			{
				case 0:
					vertices[index+0] = baseVerts[5]+position;
					vertices[index+1] = baseVerts[6]+position;
					vertices[index+2] = baseVerts[2]+position;
					vertices[index+3] = baseVerts[1]+position;
					break;
				case 1:
					vertices[index+0] = baseVerts[7]+position;
					vertices[index+1] = baseVerts[4]+position;
					vertices[index+2] = baseVerts[0]+position;
					vertices[index+3] = baseVerts[3]+position;
					break;
				case 2:
					vertices[index+0] = baseVerts[7]+position;
					vertices[index+1] = baseVerts[6]+position;
					vertices[index+2] = baseVerts[5]+position;
					vertices[index+3] = baseVerts[4]+position;
					break;
				case 3:
					vertices[index+0] = baseVerts[0]+position;
					vertices[index+1] = baseVerts[1]+position;
					vertices[index+2] = baseVerts[2]+position;
					vertices[index+3] = baseVerts[3]+position;
					break;
				case 4:
					vertices[index+0] = baseVerts[4]+position;
					vertices[index+1] = baseVerts[5]+position;
					vertices[index+2] = baseVerts[1]+position;
					vertices[index+3] = baseVerts[0]+position;
					break;
				case 5:
					vertices[index+0] = baseVerts[6]+position;
					vertices[index+1] = baseVerts[7]+position;
					vertices[index+2] = baseVerts[3]+position;
					vertices[index+3] = baseVerts[2]+position;
					break;
				default:
					vertices[index+0] = float3.zero;
					vertices[index+1] = float3.zero;
					vertices[index+2] = float3.zero;
					vertices[index+3] = float3.zero;
					Debug.Log("bad index!");
					break;
			}

			return 4;
		}

		//	Triangles are always the same set, offset to vertex index
		int GetTris(int index, int vertIndex)
		{
			triangles[index+0] = 3 + vertIndex; 
			triangles[index+1] = 1 + vertIndex; 
			triangles[index+2] = 0 + vertIndex; 
			triangles[index+3] = 3 + vertIndex; 
			triangles[index+4] = 2 + vertIndex; 
			triangles[index+5] = 1 + vertIndex;

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
				GetTris(triIndex+triOffset, vertIndex+vertOffset);
				triIndex += 6;
				GetVerts(0, pos, vertIndex+vertOffset);
				vertIndex +=  4;
			}
			if(faces[i].left == 1)
			{
				GetTris(triIndex+triOffset, vertIndex+vertOffset);
				triIndex += 6;
				GetVerts(1, pos, vertIndex+vertOffset);
				vertIndex +=  4;
			}
			if(faces[i].up == 1)
			{
				GetTris(triIndex+triOffset, vertIndex+vertOffset);
				triIndex += 6;
				GetVerts(2, pos, vertIndex+vertOffset);
				vertIndex +=  4;
			}
			if(faces[i].down == 1)
			{
				GetTris(triIndex+triOffset, vertIndex+vertOffset);
				triIndex += 6;
				GetVerts(3, pos, vertIndex+vertOffset);
				vertIndex +=  4;
			}
			if(faces[i].forward == 1)
			{
				GetTris(triIndex+triOffset, vertIndex+vertOffset);
				triIndex += 6;
				GetVerts(4, pos, vertIndex+vertOffset);
				vertIndex +=  4;
			}
			if(faces[i].back == 1)
			{
				GetTris(triIndex+triOffset, vertIndex+vertOffset);
				triIndex += 6;
				GetVerts(5, pos, vertIndex+vertOffset);
				vertIndex +=  4;
			}	
		}
	}


	public struct Vertices
    {
        public float3 v0; 
        public float3 v1; 
        public float3 v2; 
        public float3 v3; 
        public float3 v4; 
        public float3 v5; 
		public float3 v6; 
		public float3 v7; 

		public Vertices(bool param)
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
					default:
						Debug.Log("bad index!");
						return v0;
                }
            }
        }
    }

	/*struct MeshGenerator
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

		public MeshGenerator(byte param)
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

		void BuildVertArray(NativeArray<float3> array, float3 a, float3 b, float3 c, float3 d)
		{
			array[0] = a;
			array[1] = b;
			array[2] = c;
			array[3] = d;
		}

		void BuildTriArray(NativeArray<int> array, int a, int b, int c, int d, int e, int f)
		{
			array[0] = a;
			array[1] = b;
			array[2] = c;
			array[3] = d;
			array[4] = e;
			array[5] = f;
		}
		
		//	TODO build arrays only once, store in job as struct of arrays (use fancy to-array thing).
		//	Just reference this class in GetMesh() to build that struct
		public void Vertices(NativeArray<float3> array, int faceInt, float3 offset)
		{	
			switch(faceInt)
			{
				case 0: 	BuildVertArray(array, v5+offset, v6+offset, v2+offset, v1+offset); break;
				case 1: 	BuildVertArray(array, v7+offset, v4+offset, v0+offset, v3+offset); break;
				case 2: 	BuildVertArray(array, v7+offset, v6+offset, v5+offset, v4+offset); break;
				case 3: 	BuildVertArray(array, v0+offset, v1+offset, v2+offset, v3+offset); break;
				case 4:		BuildVertArray(array, v4+offset, v5+offset, v1+offset, v0+offset); break;
				case 5: 	BuildVertArray(array, v6+offset, v7+offset, v3+offset, v2+offset); break;
				default: 	BuildVertArray(array, float3.zero, float3.zero, float3.zero, float3.zero); break;
			}		
		}

		public void Triangles(NativeArray<int> array, int offset)
		{
			BuildTriArray(array, 3+offset, 1+offset, 0+offset, 3+offset, 2+offset, 1+offset);
		}
	}*/

	public Mesh GetMesh(int batchSize, Faces[] exposedFaces, int faceCount)
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
			//meshGenerator = new MeshGenerator(0),
			chunkSize = chunkSize,

			baseVerts = new Vertices(true)
		};

		//	Run job
		JobHandle handle = job.Schedule(faces.Length, batchSize);
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
