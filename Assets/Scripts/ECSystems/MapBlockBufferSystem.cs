using System;
using UnityEngine;
using Unity.Jobs;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Transforms;
using MyComponents;

//	Get y buffer for block generation based on adjacent drawing buffer, calculate array lengths and offsets for block and mesh generation
[UpdateAfter(typeof(MapDrawBufferSystem))]
public class MapBlockBufferSystem : ComponentSystem
{
    EntityManager entityManager;

	int cubeSize;

	ComponentGroup blockBufferGroup;

    protected override void OnCreateManager()
    {
        entityManager = World.Active.GetOrCreateManager<EntityManager>();
		cubeSize = TerrainSettings.cubeSize;

		EntityArchetypeQuery blockBufferQuery = new EntityArchetypeQuery{
            None  	= new ComponentType[] { typeof(Tags.EdgeBuffer), typeof(Tags.OuterBuffer) },
			All  	= new ComponentType[] { typeof(MapSquare), typeof(Tags.SetBlockBuffer) }
		};
		blockBufferGroup = GetComponentGroup(blockBufferQuery);
    }

    protected override void OnUpdate()
    {
        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);

        ArchetypeChunkEntityType 						entityType 		= GetArchetypeChunkEntityType();
		ArchetypeChunkComponentType<MapSquare> 			mapSquareType 	= GetArchetypeChunkComponentType<MapSquare>();
		ArchetypeChunkComponentType<AdjacentSquares> 	adjacentType 	= GetArchetypeChunkComponentType<AdjacentSquares>();

        NativeArray<ArchetypeChunk> chunks = blockBufferGroup.CreateArchetypeChunkArray(Allocator.TempJob);

		for(int c = 0; c < chunks.Length; c++)
		{
			ArchetypeChunk chunk = chunks[c];

			NativeArray<Entity> 			entities    = chunk.GetNativeArray(entityType);
			NativeArray<MapSquare> 			mapSquares 	= chunk.GetNativeArray(mapSquareType);
			NativeArray<AdjacentSquares> 	adjacent 	= chunk.GetNativeArray(adjacentType);
			
			for(int e = 0; e < entities.Length; e++)
			{
				Entity entity = entities[e];

                //  Get adjacent map square entities
				BlockBuffer(entity, mapSquares[e], adjacent[e], commandBuffer);

				//  Set block buffer next
                commandBuffer.RemoveComponent<Tags.SetBlockBuffer>(entity);
            }
        }
    
    	commandBuffer.Playback(entityManager);
		commandBuffer.Dispose();

    	chunks.Dispose();
    }

	void BlockBuffer(Entity entity, MapSquare mapSquare, AdjacentSquares adjacent, EntityCommandBuffer commandBuffer)
	{
		int topBuffer 		= mapSquare.topBlock;
		int bottomBuffer 	= mapSquare.bottomBlock;

		//	Find highest/lowest in 3x3 squares
		for(int i = 0; i < 4; i++)
		{
			MapSquare adjacentSquare = entityManager.GetComponentData<MapSquare>(adjacent[i]);
			int adjacentTop 	= adjacentSquare.topDrawBuffer;
			int adjacentBottom 	= adjacentSquare.bottomDrawBuffer;

			if(adjacentTop > topBuffer) topBuffer = adjacentTop;
			if(adjacentBottom < bottomBuffer) bottomBuffer = adjacentBottom;
		}

		MapSquare updateSquare = mapSquare;

		//	Top and bottom block levels to generate blocks
		updateSquare.topBlockBuffer 	= topBuffer 	+ 2;
		updateSquare.bottomBlockBuffer 	= bottomBuffer 	- 1;

		//	Calculate iteration length for block generation
		int blockGenerationHeight = (updateSquare.topBlockBuffer - updateSquare.bottomBlockBuffer)+1;
		updateSquare.blockGenerationArrayLength = blockGenerationHeight * (cubeSize*cubeSize);

		//	Calculate iteration length and offset for mesh drawing
		int drawHeight = (updateSquare.topDrawBuffer - updateSquare.bottomDrawBuffer)+1;
		updateSquare.drawArrayLength = drawHeight * (cubeSize * cubeSize);
		updateSquare.drawIndexOffset = Util.Flatten(0, updateSquare.bottomDrawBuffer - updateSquare.bottomBlockBuffer, 0, cubeSize);
		
		CustomDebugTools.DrawBufferDebug(entity, updateSquare);

		commandBuffer.SetComponent<MapSquare>(entity, updateSquare);
	}
}