using Unity.Entities;
using Unity.Mathematics;

namespace MyComponents
{
	public struct MapSquare : IComponentData { }
	[InternalBufferCapacity(0)]
	public struct Height : IBufferElementData
	{
		public int index; 
		public int height;
		//public float2 localPosition;
	}

	[InternalBufferCapacity(100)]
	public struct MapCube : IBufferElementData
	{
		public int yPos;
	}
	[InternalBufferCapacity(0)]
	public struct Block : IBufferElementData
	{
		public int index; 
		public int type;
		public float3 localPosition;
	}
}

namespace Tags
{
	//public struct DoNotDraw : IComponentData { }

	public struct CreateCubes : IComponentData { }
	public struct GenerateBlocks : IComponentData { }
	public struct DrawMesh : IComponentData { }
	public struct MeshDrawn : IComponentData { }
}