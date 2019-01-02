using Unity.Entities;
using Unity.Mathematics;

namespace MyComponents
{
	public enum CubeComposition { MIXED, SOLID, AIR};
	public enum SlopeType { NOTSLOPED, FLAT, INNERCORNER, OUTERCORNER }
	public enum SlopeFacing { NWSE, SWNE }

	public struct MapSquare : IComponentData
	{
		public int highestVisibleBlock;
		public int lowestVisibleBlock;
	}
	[InternalBufferCapacity(0)]
	public struct Terrain : IBufferElementData
	{
		public int height;
		public TerrainTypes type;
	}

	public struct AdjacentSquares : IComponentData
	{
		public Entity right;
		public Entity left;
		public Entity front;
		public Entity back;
		public Entity frontRight;
		public Entity frontLeft;
		public Entity backRight;
		public Entity backLeft;

		public Entity this[int vert]
		{
			get
			{
				switch (vert)
				{
					case 0: return right;
					case 1: return left;
					case 2: return front;
					case 3: return back;
					case 4: return frontRight;
					case 5: return frontLeft;
					case 6: return backRight;
					case 7: return backLeft;

					default: throw new System.ArgumentOutOfRangeException("Index out of range 7: " + vert);
				}
			}
		}
		
	}

	[InternalBufferCapacity(100)]
	public struct MapCube : IBufferElementData
	{
		public int yPos;
		public int blocks;
		public CubeComposition composition;
	}
	[InternalBufferCapacity(0)]
	public struct Block : IBufferElementData
	{
		public int index; 
		public int type;
		public float3 localPosition;

		public float frontRightSlope;
		public float backRightSlope;
		public float frontLeftSlope;
		public float backLeftSlope;

		public SlopeType slopeType;
		public SlopeFacing slopeFacing;
	}
}

namespace Tags
{
	public struct GenerateTerrain : IComponentData { }
	public struct CreateCubes : IComponentData { }
	public struct SetBlocks : IComponentData { }
	public struct DrawMesh : IComponentData { }

	public struct InnerBuffer : IComponentData { }
	public struct OuterBuffer : IComponentData { }
}