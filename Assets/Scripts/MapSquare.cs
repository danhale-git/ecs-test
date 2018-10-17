using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MapTenByTenSquares
{
	Vector3 position;
	public MapSquare[] squares = new MapSquare[100];

	public MapTenByTenSquares(Vector3 position)
	{
		this.position = position;
	}

	public void AddSquare(int x, int z, MapSquare square)
	{
		int index = Util.Flatten2D(x, z, 10);
		squares[index] = square;
	}
}

public class MapSquare
{
	int size = ChunkManager.chunkSize;
	int[] blocks = new int[(int)Mathf.Pow(ChunkManager.chunkSize, 3)];

	Unity.Entities.Entity meshObject;

	public MapSquare(Unity.Entities.Entity meshObject, int[] blocks)
	{

	}
}
