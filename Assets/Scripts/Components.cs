using Unity.Entities;
using Unity.Mathematics;

namespace MyComponents
{
	//	Map chunk

	public struct MapCube : IComponentData
	{
		public float3 worldPosition;
		public Entity parentMapSquare;
	}

	[InternalBufferCapacity (0)]
	public struct Block : IBufferElementData
	{
		public int index; 
		public int type;
		public float3 localPosition;
		//public float3 parentChunkWorldPosition;
	}
	public struct CREATE : IComponentData { }
	public struct CELL : IComponentData { }
	public struct POI : IComponentData { }
	public struct HEIGHT : IComponentData { }
	public struct BLOCKS : IComponentData { }
	public struct MESH : IComponentData { }

	//	Map square

	public struct MapSquare : IComponentData
	{
		public float2 worldPosition;
	}
	[InternalBufferCapacity (0)]
	public struct Height : IBufferElementData
	{
		public int index; 
		public int height;
		public float2 localPosition;
	}


}

namespace Tags
{
	//public struct DoNotDraw : IComponentData { }

	public struct CreateCubes : IComponentData { }
	public struct GenerateBlocks : IComponentData { }
	public struct DrawMesh : IComponentData { }
}