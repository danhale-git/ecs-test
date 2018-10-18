
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

    public enum Faces { TOP, BOTTOM, RIGHT, LEFT, FRONT, BACK };

    public static class Cube
	{
		public static Vector3[] Vertices(int faceInt, float3 _offset)
		{	
			Vector3 offset = (Vector3)_offset;
			Faces face = (Faces)faceInt;
			switch(face)
			{
				case Faces.TOP: 	return new Vector3[] {v7+offset, v6+offset, v5+offset, v4+offset};
				case Faces.BOTTOM: 	return new Vector3[] {v0+offset, v1+offset, v2+offset, v3+offset};
				case Faces.RIGHT: 	return new Vector3[] {v5+offset, v6+offset, v2+offset, v1+offset};
				case Faces.LEFT: 	return new Vector3[] {v7+offset, v4+offset, v0+offset, v3+offset};
				case Faces.FRONT: 	return new Vector3[] {v4+offset, v5+offset, v1+offset, v0+offset};
				case Faces.BACK: 	return new Vector3[] {v6+offset, v7+offset, v3+offset, v2+offset};
				default: 			return null;
			}		
		}

		public static int[] Triangles(int offset)
		{
			return new int[] {3+offset, 1+offset, 0+offset, 3+offset, 2+offset, 1+offset};
		}
	}

	public static Mesh GetCube(float3 offset)
	{
		Mesh mesh = new Mesh();

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();

        for(int i = 0; i < 6; i++)
        {
            triangles.AddRange(Cube.Triangles(vertices.Count));
            vertices.AddRange(Cube.Vertices(i, offset));
        }

		mesh.SetVertices(vertices);
		mesh.SetTriangles(triangles, 0);
	    mesh.RecalculateNormals();
		UnityEditor.MeshUtility.Optimize(mesh);

		return mesh;
	}
}
