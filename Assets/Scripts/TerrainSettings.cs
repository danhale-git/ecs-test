using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum TerrainTypes { DIRT, GRASS, CLIFF }

public static class TerrainSettings
{
	public static int mapSquareWidth = 12;
	public static int viewDistance = 8;

	//	Must always be at >= squareWidth
	public static int terrainHeight = 16;
	public static int seed = 5678;

	public static float cellFrequency = 0.02f;
	public static float cellEdgeSmoothing = 10.0f;

	public static int BiomeIndex(float noise)
	{
		if(noise > 0.5f) return 1;
		return 0;
	}
}
