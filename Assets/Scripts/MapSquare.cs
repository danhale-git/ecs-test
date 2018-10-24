public struct MapSquare
{
	enum Generated { NONE, CELL, POI, HEIGHT, BLOCKS, MESH }
    System.Collections.Generic.List<Unity.Entities.Entity> meshObject;

	public int[] heightMap;

    public MapSquare(int[] heightMap)
    {
		this.meshObject = new System.Collections.Generic.List<Unity.Entities.Entity>();
		this.heightMap = heightMap;
    }
}
