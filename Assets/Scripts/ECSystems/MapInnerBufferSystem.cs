using System;
using UnityEngine;
using Unity.Jobs;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Transforms;
using MyComponents;

//	Get y buffer for mesh drawing based on adjacent top/bottom blocks
[UpdateAfter(typeof(MapAdjacentSystem))]
public class MapInnerBufferSystem : ComponentSystem
{
    EntityManager entityManager;

	int cubeSize;

	EntityArchetypeQuery drawBufferQuery;

	ArchetypeChunkEntityType 						entityType;
	ArchetypeChunkComponentType<MapSquare> 			mapSquareType;
	ArchetypeChunkComponentType<AdjacentSquares> 	adjacentType;

    protected override void OnCreateManager()
    {
        entityManager = World.Active.GetOrCreateManager<EntityManager>();
		cubeSize = TerrainSettings.cubeSize;

		drawBufferQuery = new EntityArchetypeQuery
		{
			Any 	= Array.Empty<ComponentType>(),
            None  	= new ComponentType[] { typeof(Tags.EdgeBuffer) },
			All  	= new ComponentType[] { typeof(MapSquare), typeof(Tags.SetDrawBuffer) }
		};
    }

    protected override void OnUpdate()
    {
        entityType 		= GetArchetypeChunkEntityType();
		mapSquareType 	= GetArchetypeChunkComponentType<MapSquare>();
		adjacentType 	= GetArchetypeChunkComponentType<AdjacentSquares>();

        NativeArray<ArchetypeChunk> chunks = entityManager.CreateArchetypeChunkArray(
            drawBufferQuery,
            Allocator.TempJob
            );

        if(chunks.Length == 0) chunks.Dispose();
        else
			BufferMeshDrawing(chunks);
    }

    void BufferMeshDrawing(NativeArray<ArchetypeChunk> chunks)
    {
        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);

		for(int c = 0; c < chunks.Length; c++)
		{
			ArchetypeChunk chunk = chunks[c];

			NativeArray<Entity> 			entities    = chunk.GetNativeArray(entityType);
			NativeArray<MapSquare> 			mapSquares 	= chunk.GetNativeArray(mapSquareType);
			NativeArray<AdjacentSquares> 	adjacent 	= chunk.GetNativeArray(adjacentType);
			
			for(int e = 0; e < entities.Length; e++)
			{
				Entity entity = entities[e];
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

		//	Top and bottom block levels to draw mesh
		updateSquare.topDrawBuffer		= topBuffer + 1;
		updateSquare.bottomDrawBuffer	= bottomBuffer - 1;

		if(	entityManager.HasComponent<Tags.Update>(entity) 		&&
		   (updateSquare.topDrawBuffer != mapSquare.topDrawBuffer 	||
			updateSquare.bottomDrawBuffer != mapSquare.bottomDrawBuffer))
		{
			Debug.Log("buffer changed");
			BufferChange change = new BufferChange{
				topDrawBuffer = mapSquare.topDrawBuffer - updateSquare.topDrawBuffer,
				bottomDrawBuffer = mapSquare.bottomDrawBuffer - updateSquare.bottomDrawBuffer
			};

			commandBuffer.AddComponent<BufferChange>(entity, change);
		}

		commandBuffer.SetComponent<MapSquare>(entity, updateSquare);
	}
}