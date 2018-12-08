using UnityEngine;

using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Burst;

class GenerateMeshJobSystem
{
	public Mesh GetMesh(int batchSize, Faces[] exposedFaces, int faceCount)
	{
		int chunkSize = MapManager.chunkSize;

		//	Determine vertex and triangle arrays using face count
		NativeArray<float3> vertices = new NativeArray<float3>(faceCount * 4, Allocator.TempJob);
		NativeArray<int> triangles = new NativeArray<int>(faceCount * 6, Allocator.TempJob);

		NativeArray<Faces> faces = new NativeArray<Faces>(exposedFaces.Length, Allocator.TempJob);
		faces.CopyFrom(exposedFaces);

		var job = new GenerateMeshJob()
		{
			vertices = vertices,
			triangles = triangles,
			faces = faces,

			util = new JobUtil(),
			//meshGenerator = new MeshGenerator(0),
			chunkSize = chunkSize,

			baseVerts = new CubeVertices(true)
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
