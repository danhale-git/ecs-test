using Unity.Mathematics;
using UnityEngine;

public static class BlockTypes
{
	public static readonly float4[] color = new float4[]
	{
		new float4(1,1,1, 1),				//	air
		new float4(0.6f, 0.5f, 0.4f, 1),	//	dirt
		new float4(0.2f, 0.5f, 0, 1),	//	grass
		new float4(0.5f, 0.5f, 0.5f, 1)		//	stone
	};

	public static readonly int[] sloped = new int[]
	{
		1,
		1,
		1,
		1
	};

	public static readonly int[] translucent = new int[]
	{
		1,
		0,
		0,
		0
	};
}
