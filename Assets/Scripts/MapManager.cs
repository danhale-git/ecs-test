using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MapManager
{
	int chunkSize;
	//int chunkSizePlusTwo;

	static FastNoiseJobSystem terrain;

	static Dictionary<Vector3, MapSquare> map = new Dictionary<Vector3, MapSquare>();

	public MapManager()
	{
		chunkSize = ChunkManager.chunkSize;
		//chunkSizePlusTwo = ChunkManager.chunkSizePlusTwo;

		//	Create job systems
        terrain = new FastNoiseJobSystem();
	}

	public int[] GenerateMapSquare(Vector3 position)
	{
		//	Noise
        float[] noise = terrain.GetSimplexMatrix(position, chunkSize, 5678, 0.05f);

        //  Noise to height map
        int[] heightMap = new int[noise.Length];
        for(int i = 0; i < noise.Length; i++)
            heightMap[i] = (int)(noise[i] * chunkSize);

		return heightMap;
	}
}
