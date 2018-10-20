public struct Faces
{
	public readonly int right, left, up, down, forward, back;
	//public readonly int[] array;
	public readonly int count;
	public Faces(int right, int left, int up, int down, int forward, int back)
	{
		this.right = right;
		this.left = left;
		this.up = up;
		this.down = down;
		this.forward = forward;
		this.back = back;

		//array = new int[] { right, left, up, down, forward, back };
		
		count = 0;
		count += right;
		count += left;
		count += up;
		count += down;
		count += forward;
		count += back;
	}
}