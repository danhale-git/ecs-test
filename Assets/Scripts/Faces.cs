public struct Faces
{
	public int right, left, up, down, front, back;
	public int count;
	public int faceIndex, triIndex, vertIndex;
	
	public void SetCount()
	{
		count = right + left + up + down + front + back;
	}

	public int this[int side]
	{
		get
		{
			switch(side)
			{
				case 0: return right;
				case 1: return left;
				case 2: return front;
				case 3: return back;
				case 4: return up;
				case 5: return down;
				default: throw new System.ArgumentOutOfRangeException("Index out of range 7: " + side);
			}
		}

		set
		{
			switch(side)
			{
				case 0: right = value; break;
				case 1: left = value; break;
				case 2: front = value; break;
				case 3: back = value; break;
				case 4: up = value; break;
				case 5: down = value; break;
				default: throw new System.ArgumentOutOfRangeException("Index out of range 7: " + side);
			}
		}
	}
}