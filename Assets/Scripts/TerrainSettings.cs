using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum TerrainTypes { DIRT, GRASS, CLIFF }

public static class TerrainSettings
{
	public static int cubeSize = 16;
	public static int viewDistance = 4;
	//	Must always be at >= cubeSize
	public static int terrainHeight = 32;
	public static int terrainStretch = 64;
	public static int seed = 5678;
	public static float frequency = 0.01f;
	public static float cellGradientPeturb = 5.0f;

	public static int BiomeIndex(float noise)
	{
		if(noise > 0.5f) return 1;
		return 0;
	}
}
