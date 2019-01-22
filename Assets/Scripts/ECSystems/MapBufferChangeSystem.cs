using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using MyComponents;

//	Generate 3D block data from 2D terrain data
[UpdateAfter(typeof(MapOuterBufferSystem))]
public class MapBufferChangeSystem : ComponentSystem
{
	EntityManager entityManager;

	int cubeSize;

	ArchetypeChunkEntityType 					entityType;
	ArchetypeChunkComponentType<MapSquare>		mapSquareType;
	ArchetypeChunkBufferType<Block> 			blocksType;
	ArchetypeChunkBufferType<Topology> 		heightmapType;	

	EntityArchetypeQuery mapSquareQuery;

	protected override void OnCreateManager()
	{
		entityManager = World.Active.GetOrCreateManager<EntityManager>();
		cubeSize = TerrainSettings.cubeSize;

		mapSquareQuery = new EntityArchetypeQuery
		{
			Any 	= Array.Empty<ComponentType>(),
			None  	= new ComponentType[] { typeof(Tags.EdgeBuffer), typeof(Tags.OuterBuffer) },
			All  	= new ComponentType[] { typeof(MapSquare), typeof(Tags.BufferChanged) }
		};
	}

	protected override void OnUpdate()
	{
		entityType 		= GetArchetypeChunkEntityType();
		mapSquareType	= GetArchetypeChunkComponentType<MapSquare>();

		blocksType 		= GetArchetypeChunkBufferType<Block>();
        heightmapType 	= GetArchetypeChunkBufferType<Topology>();

		NativeArray<ArchetypeChunk> chunks = entityManager.CreateArchetypeChunkArray(
			mapSquareQuery,
			Allocator.TempJob
			);

		if(chunks.Length == 0) chunks.Dispose();
		else UpdateBuffers(chunks);
	}

	void UpdateBuffers(NativeArray<ArchetypeChunk> chunks)
	{
		EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);

		for(int c = 0; c < chunks.Length; c++)
		{
			ArchetypeChunk chunk = chunks[c];

			NativeArray<Entity> entities 				= chunk.GetNativeArray(entityType);
			NativeArray<MapSquare> mapSquares			= chunk.GetNativeArray(mapSquareType);
			BufferAccessor<Block> blockAccessor 		= chunk.GetBufferAccessor(blocksType);
            BufferAccessor<Topology> heightmapAccessor 	= chunk.GetBufferAccessor(heightmapType);
			
			for(int e = 0; e < entities.Length; e++)
			{
				Entity entity 						= entities[e];
				DynamicBuffer<Block> blockBuffer 	= blockAccessor[e];
                DynamicBuffer<Topology> heightmap		= heightmapAccessor[e];

				MapSquare mapSquare = entityManager.GetComponentData<MapSquare>(entity);

				float sliceLength = math.pow(cubeSize, 2);

				float bottomSliceCount 	= blockBuffer[0].localPosition.y - mapSquare.bottomBlockBuffer;
				float topSliceCount 	= mapSquare.topBlockBuffer - blockBuffer[blockBuffer.Length-1].localPosition.y;

				NativeArray<Block> oldBlocks = new NativeArray<Block>(blockBuffer.Length, Allocator.TempJob);
				oldBlocks.CopyFrom(blockBuffer.AsNativeArray());

				DynamicBuffer<Block> newBuffer = commandBuffer.SetBuffer<Block>(entity);
				newBuffer.ResizeUninitialized(mapSquare.blockGenerationArrayLength);

				int bottomOffset 	= (int)	(bottomSliceCount	* sliceLength);
				int topOffset		= (int)	(topSliceCount		* sliceLength);
				int topStart		= 		(bottomOffset + oldBlocks.Length);

				/*Debug.Log("bottomOffset: "+bottomOffset);
				Debug.Log("topOffset: "+topOffset);
				Debug.Log("topStart: "+topStart); */


				/*NativeArray<Block> prepend = GetBlocks(mapSquare, heightmap, bottomOffset);
				newBuffer.AddRange(prepend);
				prepend.Dispose();

				NativeArray<Block> copy = GetBlocks(mapSquare, heightmap, oldBlocks.Length, bottomOffset);
				newBuffer.AddRange(copy);
				copy.Dispose();

				NativeArray<Block> append = GetBlocks(mapSquare, heightmap, topOffset, topStart);
				newBuffer.AddRange(append); 
				append.Dispose(); */

				for(int i = 0; i < bottomOffset; i++)
				{
					newBuffer[i] = GetBlock(i, mapSquare, heightmap);
				} 

				for(int i = 0; i < oldBlocks.Length; i++)
				{
					newBuffer[i+bottomOffset] = oldBlocks[i];
				} 
				
				for(int i = 0; i < topOffset; i++)
				{
					int index = i + topStart;
					newBuffer[index] = GetBlock(index, mapSquare, heightmap);
				}    

				commandBuffer.RemoveComponent<Tags.BufferChanged>(entity);

				oldBlocks.Dispose();
			}
		}
		
		commandBuffer.Playback(entityManager);
		commandBuffer.Dispose();

		chunks.Dispose();
	}

	NativeArray<Block> GetBlocks(MapSquare mapSquare, DynamicBuffer<Topology> heightMap, int arrayLength, int offset = 0)
	{
		//	Block data for all cubes in the map square
		var blocks = new NativeArray<Block>(arrayLength, Allocator.TempJob);

		BlocksJob job = new BlocksJob{
			blocks = blocks,
			mapSquare = mapSquare,
			heightMap = heightMap,
			cubeSize = cubeSize,
			util = new JobUtil(),
			offset = offset
		};
		
		job.Schedule(arrayLength, 1).Complete(); 

		return blocks;
	}

	public Block GetBlock(int index, MapSquare mapSquare, DynamicBuffer<Topology> heightMap)
	{
		float3 pos = Util.Unflatten(index, cubeSize);

		float3 position = pos + new float3(0, mapSquare.bottomBlockBuffer, 0);

		int hMapIndex = Util.Flatten2D((int)position.x, (int)position.z, cubeSize);
		int type = 0;

		if(position.y <= heightMap[hMapIndex].height)
		{
			switch(heightMap[hMapIndex].type)
			{
				case TerrainTypes.DIRT:
					type = 1; break;
				case TerrainTypes.GRASS:
					type = 2; break;
				case TerrainTypes.CLIFF:
					type = 3; break;
			}
		}

		float3 worldPosition = position + mapSquare.position;
		int debug = 0;
		/*if(position.y == heightMap[hMapIndex].height && worldPosition.x == 84 && worldPosition.z == 641)
			debug = 1;
		if(position.y == heightMap[hMapIndex].height && worldPosition.x == 83 && worldPosition.z == 641)
			debug = 2; */

		return new Block
		{
			debug = debug,

			type = type,
			localPosition = position,
			worldPosition = worldPosition
		};
	}
}
