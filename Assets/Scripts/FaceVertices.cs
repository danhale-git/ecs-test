using Unity.Mathematics;

public struct FaceVertices
{
	public readonly float3 v0, v1, v2, v3;
	public FaceVertices(float3 v0, float3 v1, float3 v2, float3 v3)
	{
		this.v0 = v0;
		this.v1 = v1;
		this.v2 = v2;
		this.v3 = v3;
	}

	public float3 this[int side]
	{
		get
		{
			switch(side)
			{
				case 0: return v0;
				case 1: return v1;
				case 2: return v2;
				case 3: return v3;
				default: throw new System.ArgumentOutOfRangeException("Index out of range 3: " + side);
			}
		}
	}
}