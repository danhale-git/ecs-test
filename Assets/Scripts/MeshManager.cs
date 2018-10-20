
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;


public static class MeshManager
{
    //	Cube corners
	readonly static Vector3 v0 = new Vector3( 	-0.5f, -0.5f,	 0.5f );	//	left bottom front
	readonly static Vector3 v2 = new Vector3( 	 0.5f, -0.5f,	-0.5f );	//	right bottom back
	readonly static Vector3 v3 = new Vector3( 	-0.5f, -0.5f,	-0.5f ); 	//	left bottom back
	readonly static Vector3 v1 = new Vector3( 	 0.5f, -0.5f,	 0.5f );	//	right bottom front
	readonly static Vector3 v4 = new Vector3( 	-0.5f,  0.5f,	 0.5f );	//	left top front
	readonly static Vector3 v5 = new Vector3( 	 0.5f,  0.5f,	 0.5f );	//	right top front
	readonly static Vector3 v6 = new Vector3( 	 0.5f,  0.5f,	-0.5f );	//	right top back
	readonly static Vector3 v7 = new Vector3( 	-0.5f,  0.5f,	-0.5f );	//	left top back

    public enum FacesEnum { RIGHT, LEFT, UP, DOWN, FORWARD, BACK };

    public static class Cube
	{
		public static Vector3[] Vertices(int faceInt, float3 _offset)
		{	
			Vector3 offset = (Vector3)_offset;
			FacesEnum face = (FacesEnum)faceInt;
			switch(face)
			{
				case FacesEnum.RIGHT: 	return new Vector3[] {v5+offset, v6+offset, v2+offset, v1+offset};
				case FacesEnum.LEFT: 	return new Vector3[] {v7+offset, v4+offset, v0+offset, v3+offset};
				case FacesEnum.UP: 	return new Vector3[] {v7+offset, v6+offset, v5+offset, v4+offset};
				case FacesEnum.DOWN: 	return new Vector3[] {v0+offset, v1+offset, v2+offset, v3+offset};
				case FacesEnum.FORWARD: 	return new Vector3[] {v4+offset, v5+offset, v1+offset, v0+offset};
				case FacesEnum.BACK: 	return new Vector3[] {v6+offset, v7+offset, v3+offset, v2+offset};
				default: 			return null;
			}		
		}

		public static int[] Triangles(int offset)
		{
			return new int[] {3+offset, 1+offset, 0+offset, 3+offset, 2+offset, 1+offset};
		}
	}

	public static Mesh GetMesh(float3 offset, Faces[] exposedFaces, int[] blocks)
	{
		Mesh mesh = new Mesh();
		int chunkSize = ChunkManager.chunkSize;

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();

		Debug.Log(blocks.Length);


		for(int i = 0; i < math.pow(chunkSize, 3); i++)
        {
			if(blocks[i] == 0) continue;
			Debug.Log("created block");

			bool drewSomething = false;


			float3 pos = Util.Unflatten(i, chunkSize);

			if(exposedFaces[i].right == 1)
			{
				triangles.AddRange(Cube.Triangles(vertices.Count));
				vertices.AddRange(Cube.Vertices(0, pos));
				drewSomething = true;
			}
			if(exposedFaces[i].left == 1)
			{
				triangles.AddRange(Cube.Triangles(vertices.Count));
				vertices.AddRange(Cube.Vertices(1, pos));
				drewSomething = true;
			}
			if(exposedFaces[i].up == 1)
			{
				triangles.AddRange(Cube.Triangles(vertices.Count));
				vertices.AddRange(Cube.Vertices(2, pos));
				drewSomething = true;
			}
			if(exposedFaces[i].down == 1)
			{
				triangles.AddRange(Cube.Triangles(vertices.Count));
				vertices.AddRange(Cube.Vertices(3, pos));
				drewSomething = true;
			}
			if(exposedFaces[i].forward == 1)
			{
				triangles.AddRange(Cube.Triangles(vertices.Count));
				vertices.AddRange(Cube.Vertices(4, pos));
				drewSomething = true;
			}
			if(exposedFaces[i].back == 1)
			{
				triangles.AddRange(Cube.Triangles(vertices.Count));
				vertices.AddRange(Cube.Vertices(5, pos));
				drewSomething = true;
			}
        	if(drewSomething)Debug.Log("drew a face on block");

		}


		mesh.SetVertices(vertices);
		mesh.SetTriangles(triangles, 0);
	    mesh.RecalculateNormals();
		UnityEditor.MeshUtility.Optimize(mesh);

		return mesh;
	}


}
