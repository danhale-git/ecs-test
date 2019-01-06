public struct Faces
{
	public enum Exp { HIDDEN, FULL, HALF }

	public int right, left, up, down, front, back;
	public int count;
	public int faceIndex, triIndex, vertIndex;
	
	public void SetCount()
	{
		if(right	> 0) count++;
		if(left		> 0) count++;
		if(up		> 0) count++;
		if(down		> 0) count++;
		if(front	> 0) count++;
		if(back		> 0) count++;
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