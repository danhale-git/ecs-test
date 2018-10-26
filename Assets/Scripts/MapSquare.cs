public class MapSquare
{
    public System.Collections.Generic.List<Unity.Entities.Entity> meshObjects;
    public int[] blocks;

	public enum Stages { CREATE, CELL, POI, HEIGHT, BLOCKS, MESH }
    public Stages stage;

    public MapSquare(bool placeholder)
    {
		this.meshObjects = new System.Collections.Generic.List<Unity.Entities.Entity>();
        this.stage = 0;
        this.blocks = null;
    }
}
