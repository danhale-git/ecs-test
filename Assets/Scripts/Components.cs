using Unity.Entities;
using Unity.Mathematics;

namespace MyComponents
{
	public enum CubeComposition { MIXED, SOLID, AIR};

	public struct MapSquare : IComponentData
	{
		public int highestBlock;
		public int lowestBlock;
	}
	[InternalBufferCapacity(0)]
	public struct Height : IBufferElementData
	{
		public int height;
	}

	public struct AdjacentEntities : IComponentData
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
	public struct CreateCubes : IComponentData { }
	public struct GenerateBlocks : IComponentData { }
	public struct DrawMesh : IComponentData { }

	public struct InnerBuffer : IComponentData { }
	public struct OuterBuffer : IComponentData { }
}