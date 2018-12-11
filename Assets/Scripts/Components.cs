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

	[InternalBufferCapacity(0)]
	public struct Block : IBufferElementData
	{
		public int index; 
		public int type;
		public float3 localPosition;
		//public float3 parentChunkWorldPosition;
	}

	[InternalBufferCapacity(100)]
	public struct CubePosition : IBufferElementData
	{
		public int y;
	}

	/*public struct CubeCount : IComponentData
	{
		public int count;
	}*/

	//	Map square

	public struct MapSquare : IComponentData
	{
		public float2 worldPosition;
	}
	[InternalBufferCapacity(0)]
	public struct Height : IBufferElementData
	{
		public int index; 
		public int height;
		//public float2 localPosition;
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