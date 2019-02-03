using System;
using UnityEngine;
using Unity.Jobs;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Transforms;
using MyComponents;

//	Get y buffer for mesh drawing based on adjacent top/bottom blocks
[UpdateAfter(typeof(UpdateGroups.NewMapSquareUpdateGroup))]
[UpdateAfter(typeof(MapUpdateSystem))]
public class MapDrawBoundsSystem : ComponentSystem
{
    EntityManager entityManager;

	int squareWidth;

	ComponentGroup drawBufferGroup;

    protected override void OnCreateManager()
    {
        entityManager = World.Active.GetOrCreateManager<EntityManager>();
		
		squareWidth = TerrainSettings.mapSquareWidth;

		EntityArchetypeQuery drawBufferQuery = new EntityArchetypeQuery{
            None 	= new ComponentType[] { typeof(Tags.EdgeBuffer) },
			All 	= new ComponentType[] { typeof(MapSquare), typeof(Tags.SetDrawBuffer) }
		};
		drawBufferGroup = GetComponentGroup(drawBufferQuery);
    }

    protected override void OnUpdate()
    {
        EntityCommandBuffer 		commandBuffer 	= new EntityCommandBuffer(Allocator.Temp);
        NativeArray<ArchetypeChunk> chunks 			= drawBufferGroup.CreateArchetypeChunkArray(Allocator.TempJob);

		ArchetypeChunkEntityType 						entityType 		= GetArchetypeChunkEntityType();
		ArchetypeChunkComponentType<MapSquare> 			mapSquareType 	= GetArchetypeChunkComponentType<MapSquare>();
		ArchetypeChunkComponentType<AdjacentSquares> 	adjacentType 	= GetArchetypeChunkComponentType<AdjacentSquares>();

		for(int c = 0; c < chunks.Length; c++)
		{
			ArchetypeChunk chunk = chunks[c];

			NativeArray<Entity> 			entities    = chunk.GetNativeArray(entityType);
			NativeArray<MapSquare> 			mapSquares 	= chunk.GetNativeArray(mapSquareType);
			NativeArray<AdjacentSquares> 	adjacent 	= chunk.GetNativeArray(adjacentType);
			
			for(int e = 0; e < entities.Length; e++)
			{
				Entity 			entity 			= entities[e];
				AdjacentSquares adjacentSquares = adjacent[e];

                //	Check top and bottom limits for drawing map square
				DrawBuffer(entity, mapSquares[e], adjacentSquares, commandBuffer);

				//  Set block buffer next
                commandBuffer.RemoveComponent<Tags.SetDrawBuffer>(entity);
            }
        }
    
    	commandBuffer.Playback(entityManager);
		commandBuffer.Dispose();

    	chunks.Dispose();
    }

	void DrawBuffer(Entity entity, MapSquare mapSquare, AdjacentSquares adjacent, EntityCommandBuffer commandBuffer)
	{
		int topBuffer 		= mapSquare.topBlock;
		int bottomBuffer 	= mapSquare.bottomBlock;

		//	Find highest/lowest in 3x3 squares
		for(int i = 0; i < 4; i++)
		{
			MapSquare adjacentSquare = entityManager.GetComponentData<MapSquare>(adjacent[i]);

			int adjacentTop 	= adjacentSquare.topBlock;
			int adjacentBottom 	= adjacentSquare.bottomBlock;

			if(adjacentTop > topBuffer) topBuffer = adjacentTop;
			if(adjacentBottom < bottomBuffer) bottomBuffer = adjacentBottom;
		}

		MapSquare updateSquare = mapSquare;

		updateSquare.topDrawBounds		= topBuffer;
		updateSquare.bottomDrawBounds	= bottomBuffer;	

		commandBuffer.SetComponent<MapSquare>(entity, updateSquare);
	}
}