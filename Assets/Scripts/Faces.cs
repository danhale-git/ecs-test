public struct Faces
{
	public readonly int right, left, up, down, forward, back;
	public readonly int count;
	public int faceIndex;
	public Faces(int right, int left, int up, int down, int forward, int back, int faceIndex)
	{
		this.right = right;
		this.left = left;
		this.up = up;
		this.down = down;
		this.forward = forward;
		this.back = back;

		count = right + left + up + down + forward + back;

		this.faceIndex = faceIndex;
	}
	public int Count()
	{
		return right + left + up + down + forward + back;
	}
}