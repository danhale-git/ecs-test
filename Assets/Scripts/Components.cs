using Unity.Entities;
using Unity.Mathematics;

public struct Chunk : IComponentData
{
	public int index;
	public float3 chunkWorldPosition;
}

[InternalBufferCapacity (0)]
public struct Block : IBufferElementData
{
	public int blockIndex; 
	public int blockType;
	public float3 localPosition;
	public float3 parentChunkWorldPosition;
}
