using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class TerrainSettings
{
	public static int cubeSize = 12;
	public static int viewDistance = 4;
	//	Must always be at >= cubeSize
	public static int terrainHeight = 12;
	public static int terrainStretch = 36;
	public static int seed = 5678;
	public static float frequency = 0.01f;
}
