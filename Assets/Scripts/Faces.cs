public struct Faces
{
	public readonly int right, left, up, down, forward, back;
	public readonly int count;
	public int faceIndex, triIndex, vertIndex;
	public Faces(int right, int left, int up, int down, int forward, int back, int faceIndex, int triIndex, int vertIndex)
	{
		this.right 		= right;
		this.left 		= left;
		this.up 		= up;
		this.down 		= down;
		this.forward 	= forward;
		this.back 		= back;

		count = right + left + up + down + forward + back;

		this.faceIndex 	= faceIndex;
		this.triIndex 	= triIndex;
		this.vertIndex 	= vertIndex;
	}
}