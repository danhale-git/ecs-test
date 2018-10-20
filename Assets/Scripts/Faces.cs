public struct Faces
{
	public readonly int right, left, up, down, forward, back;
	public Faces(int right, int left, int up, int down, int forward, int back)
	{
		this.right = right;
		this.left = left;
		this.up = up;
		this.down = down;
		this.forward = forward;
		this.back = back;
	}
}