using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

class GenerateMeshJobSystem
{
	struct BlockMeshJob : IJob
	{
		public NativeList<float3> verts;
		public NativeList<int> tris;
		
		[ReadOnly]public int vertCount;
		[ReadOnly]public float3 position;
		[ReadOnly]public Faces exposure;
		[ReadOnly]public MeshGen mesh;

		public int localVertCount;

		public void GetValues(int side)
		{
			//	Vertices
			NativeArray<float3> v = new NativeArray<float3>(4, Allocator.Temp);
			v.CopyFrom(mesh.Vertices(side));
			
			//	Triangles
			NativeArray<int> t = new NativeArray<int>(6, Allocator.Temp);
			t.CopyFrom(mesh.Triangles(vertCount + localVertCount));

			localVertCount += v.Length;
			
			verts.AddRange(v);
			tris.AddRange(t);

			v.Dispose();
			t.Dispose();
		}

		//	Add vertices and triangles for exposed faces
		public void Execute()
		{
			if(exposure.right == 1)
			{
				GetValues(0);
			}
			if(exposure.left == 1)
			{
				GetValues(1);
			}
			if(exposure.up == 1)
			{
				GetValues(2);
			}
			if(exposure.down == 1)
			{
				GetValues(3);
			}
			if(exposure.forward == 1)
			{
				GetValues(4);
			}
			if(exposure.back == 1)
			{
				GetValues(5);
			}
		}
	}

	public Mesh GetMesh(Faces[] exposedFaces, int[] blocks)
	{
		int chunkSize = ChunkManager.chunkSize;
		int blockArrayLength = (int)math.pow(chunkSize, 3);

		List<int> triangles = new List<int>();
		List<Vector3> vertices = new List<Vector3>();

		int vertCount = 0;

		//MeshGen meshGen = new MeshGen(position);
		

		for(int i = 0; i < blockArrayLength; i++)
		{
			if(blocks[i] == 0) continue;

			float3 position = Util.Unflatten(i, chunkSize);

			//	counts are kept in Faces struct
			int vCount = exposedFaces[i].count * 4;
			int tCount = exposedFaces[i].count * 6;

			NativeList<float3> verts = new NativeList<float3>(vCount, Allocator.TempJob);
			NativeList<int> tris = new NativeList<int>(tCount, Allocator.TempJob);
			
			var job = new BlockMeshJob()
			{
				verts = verts,
				tris = tris,

				vertCount = vertCount,
				position = position,
				exposure = exposedFaces[i],
				mesh = new MeshGen(position),

				localVertCount = 0
			};

			vertCount += vCount;

			//	TODO: job.Schedule().Complete();
			JobHandle jobHandle = job.Schedule();
			jobHandle.Complete();

			for(int t = 0; t < tris.Length; t++)
				triangles.Add(tris[t]);

			for(int v = 0; v < verts.Length; v++)
				vertices.Add(verts[v]);

			verts.Dispose();
			tris.Dispose();
		}

		Mesh mesh = new Mesh();
		mesh.SetVertices(vertices);
		mesh.SetTriangles(triangles, 0);
		mesh.RecalculateNormals();
		UnityEditor.MeshUtility.Optimize(mesh);

		return mesh;
	}

	#region Incorrect solution
	struct GetTrianglesJob : IJobParallelFor
	{
		public int vertCount;

		[ReadOnly] public NativeArray<Faces> faces;
		[ReadOnly] public MeshGen2 mesh;

		//[ReadOnly] public int vertCount;

		//public NativeArray<int> blocks;

		[ReadOnly] public int chunkSize;
		[ReadOnly] public JobUtil util;

		public NativeArray<int> longArray;	//DEBUG

		NativeArray<int> FaceArray(Faces faces)
		{
			NativeArray<int> array = new NativeArray<int>(6, Allocator.TempJob);
			array[0] = faces.right;
			array[1] = faces.left;
			array[2] = faces.up;
			array[3] = faces.down;
			array[4] = faces.forward;
			array[5] = faces.back;
			return array;
		}

		void GetValues(int index, float3 blockPosition)//, NativeArray<float3> verts, NativeArray<int> tris)
		{
			NativeArray<int> faceArray = FaceArray(faces[index]);

			int triCount = (int)(vertCount * 1.5);

			//	Verts and tris for one block (exposed faces count * values per face)
			NativeArray<float3> verts = new NativeArray<float3>(faces[index].count*4, Allocator.TempJob);
			NativeArray<int> tris = new NativeArray<int>(faces[index].count*6, Allocator.TempJob);

			for(int f = 0; f < 6; f++)
			{
				if(faceArray[f] == 1)
				{
					//	Vertices for one face
					NativeArray<float3> vs = new NativeArray<float3>(4, Allocator.Temp);
					vs.CopyFrom(mesh.Vertices(f, blockPosition));
					
					//	Triangles for one face
					NativeArray<int> ts = new NativeArray<int>(6, Allocator.Temp);
					ts.CopyFrom(mesh.Triangles(triCount));

					//	Add range
					for(int v = 0; v > 4; v++)
					{
						verts[vertCount+v] = vs[v];
					}
					for(int t = 0; t > 6; t++)
					{
						tris[triCount+t] = ts[t];
					}

					vs.Dispose();
					ts.Dispose();
				}
			}

			faceArray.Dispose();
			verts.Dispose();
			tris.Dispose();
		}

		public void Execute(int i)
		{
			//	Get local position in heightmap
			float3 pos = util.Unflatten(i, chunkSize);
			longArray[i*2] = 0;
		}
	}

	//	TODO does this need the blocks array?
	//	Separate jobs for concatenating vert and tri arrays
	public Mesh GetChunkMesh(Faces[] exposedFaces, int[] blocks)
	{
		//	TriangleJob
		//	Add up number of verts
		//	iterate over total verts, get all verts

		//	VertexJob
		//	Add up number of tris
		//	iterate over total tris, get all tris

		//	convert arrays, done!	

		int chunkSize = ChunkManager.chunkSize;

		//	Count number of verts and tris needed
		int vertCount = 0;
		int triCount = 0;
		for(int i = 0; i < exposedFaces.Length; i++)
		{
			if(blocks[i] == 0) continue;

			vertCount += exposedFaces[i].count * 4;
			triCount += exposedFaces[i].count * 6;
		}

		//	Native arrays
		NativeArray<Faces> faces = new NativeArray<Faces>(exposedFaces.Length, Allocator.TempJob);
		faces.CopyFrom(exposedFaces);

		NativeArray<int> longArray = new NativeArray<int>(exposedFaces.Length*2, Allocator.TempJob);	//DEBUG

		var job = new GetTrianglesJob()
		{
			vertCount = 0,
			faces = faces,
			mesh = new MeshGen2(0),
			//blocks = blocks,
			chunkSize = chunkSize,
			util = new JobUtil(),

			longArray = longArray
		};

		JobHandle handle = job.Schedule(exposedFaces.Length, 1);
		handle.Complete();


		Mesh mesh = new Mesh();
		/*mesh.SetVertices(vertices);
		mesh.SetTriangles(triangles, 0);
		mesh.RecalculateNormals();
		UnityEditor.MeshUtility.Optimize(mesh);*/

		faces.Dispose();
		longArray.Dispose();


		return mesh;
	}


	struct MeshGen
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

		float3 offset;

		public MeshGen(float3 offset)
		{
			this.offset = offset;
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

		
		public float3[] Vertices(int faceInt)
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

	struct MeshGen2
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


		public MeshGen2(byte constParam)
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
	#endregion
}
