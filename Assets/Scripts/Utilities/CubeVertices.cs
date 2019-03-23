using Unity.Mathematics;

public struct CubeVertices
{
	public float3 v0; 
	public float3 v1; 
	public float3 v2; 
	public float3 v3; 
	public float3 v4; 
	public float3 v5; 
	public float3 v6; 
	public float3 v7; 

	public CubeVertices(bool param)
	{
		v0 = new float3( 	-0.5f, -0.5f,	 0.5f );	//	left bottom front;
		v1 = new float3( 	 0.5f, -0.5f,	 0.5f );	//	right bottom front;
		v2 = new float3( 	 0.5f, -0.5f,	-0.5f );	//	right bottom back;
		v3 = new float3( 	-0.5f, -0.5f,	-0.5f ); 	//	left bottom back;
		v4 = new float3( 	-0.5f,  0.5f,	 0.5f );	//	left top front;
		v5 = new float3( 	 0.5f,  0.5f,	 0.5f );	//	right top front;
		v6 = new float3( 	 0.5f,  0.5f,	-0.5f );	//	right top back;
		v7 = new float3( 	-0.5f,  0.5f,	-0.5f );	//	left top back;
	}

	public FaceVertices FaceVertices(int side)
	{
		switch(side)
		{
			case 0:	//	Right
				return new FaceVertices(
					v5 = new float3( 	 0.5f,  0.5f,	 0.5f ),	//	right top front;
					v6 = new float3( 	 0.5f,  0.5f,	-0.5f ),	//	right top back;
					v1 = new float3( 	 0.5f, -0.5f,	 0.5f ),	//	right bottom front;
					v2 = new float3( 	 0.5f, -0.5f,	-0.5f )		//	right bottom back;
				);

			case 1:	//	Left
				return new FaceVertices(
					v4 = new float3( 	-0.5f,  0.5f,	 0.5f ),	//	left top front;
					v7 = new float3( 	-0.5f,  0.5f,	-0.5f ),	//	left top back;
					v0 = new float3( 	-0.5f, -0.5f,	 0.5f ),	//	left bottom front;
					v3 = new float3( 	-0.5f, -0.5f,	-0.5f ) 	//	left bottom back;
				);

			case 2:	//	Front
				return new FaceVertices(
					v5 = new float3( 	 0.5f,  0.5f,	 0.5f ),	//	right top front;
					v4 = new float3( 	-0.5f,  0.5f,	 0.5f ),	//	left top front;
					v1 = new float3( 	 0.5f, -0.5f,	 0.5f ),	//	right bottom front;
					v0 = new float3( 	-0.5f, -0.5f,	 0.5f )		//	left bottom front;
				);

			case 3:	//	Back
				return new FaceVertices(
					v6 = new float3( 	 0.5f,  0.5f,	-0.5f ),	//	right top back;
					v7 = new float3( 	-0.5f,  0.5f,	-0.5f ),	//	left top back;
					v2 = new float3( 	 0.5f, -0.5f,	-0.5f ),	//	right bottom back;
					v3 = new float3( 	-0.5f, -0.5f,	-0.5f ) 	//	left bottom back;
				);
			default: throw new System.ArgumentOutOfRangeException("Index out of range 3: " + side);
		}
	}

	public float3 this[int vert]
	{
		get
		{
			switch (vert)
			{
				case 0: return v0;
				case 1: return v1;
				case 2: return v2;
				case 3: return v3;
				case 4: return v4;
				case 5: return v5;
				case 6: return v6;
				case 7: return v7;
				default: throw new System.ArgumentOutOfRangeException("Index out of range 7: " + vert);
			}
		}
	}
}
