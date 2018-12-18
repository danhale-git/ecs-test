using Unity.Entities;
using Unity.Mathematics;

namespace MyComponents
{
	public enum CubeComposition { MIXED, SOLID, AIR};

	public struct MapSquare : IComponentData
	{
		public int highestVisibleBlock;
		public int lowestVisibleBlock;
	}
	[InternalBufferCapacity(0)]
	public struct Height : IBufferElementData
	{
		public int height;
	}

	public struct AdjacentSquares : IComponentData
	{
		public Entity right;
		public Entity left;
		public Entity front;
		public Entity back;
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
		public float3 squareLocalPosition;
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