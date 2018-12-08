using Unity.Entities;
using Unity.Mathematics;

public struct MapChunk : IComponentData
{
	public float3 worldPosition;
}

[InternalBufferCapacity (0)]
public struct Block : IBufferElementData
{
	public int blockIndex; 
	public int blockType;
	public float3 localPosition;
	//public float3 parentChunkWorldPosition;
}

public struct CREATE : IComponentData { }
public struct CELL : IComponentData { }
public struct POI : IComponentData { }
public struct HEIGHT : IComponentData { }
public struct BLOCKS : IComponentData { }
public struct MESH : IComponentData { }

public struct MapEdge : IComponentData { }

