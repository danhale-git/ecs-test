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
		[ReadOnly]public int triangleOffset;

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
}
