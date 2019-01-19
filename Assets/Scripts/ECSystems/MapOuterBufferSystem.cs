using System;
using UnityEngine;
using Unity.Jobs;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Transforms;
using MyComponents;

//	Get y buffer for block generation based on adjacent drawing buffer, calculate array lengths and offsets for block and mesh generation
[UpdateAfter(typeof(MapInnerBufferSystem))]
public class MapOuterBufferSystem : ComponentSystem
{
    EntityManager entityManager;

	int cubeSize;

	EntityArchetypeQuery blockBufferQuery;

	ArchetypeChunkEntityType 						entityType;
    ArchetypeChunkComponentType<Position> 			positionType;
	ArchetypeChunkComponentType<MapSquare> 			mapSquareType;
	ArchetypeChunkComponentType<AdjacentSquares> 	adjacentType;

    protected override void OnCreateManager()
    {
        entityManager = World.Active.GetOrCreateManager<EntityManager>();
		cubeSize = TerrainSettings.cubeSize;

		blockBufferQuery = new EntityArchetypeQuery
		{
			Any 	= Array.Empty<ComponentType>(),
            None  	= new ComponentType[] { typeof(Tags.EdgeBuffer), typeof(Tags.OuterBuffer) },
			All  	= new ComponentType[] { typeof(MapSquare), typeof(Tags.SetBlockBuffer) }
		};
    }

    protected override void OnUpdate()
    {
        entityType 		= GetArchetypeChunkEntityType();
        positionType 	= GetArchetypeChunkComponentType<Position>();
		mapSquareType 	= GetArchetypeChunkComponentType<MapSquare>();
		adjacentType 	= GetArchetypeChunkComponentType<AdjacentSquares>();

        NativeArray<ArchetypeChunk> chunks = entityManager.CreateArchetypeChunkArray(
            blockBufferQuery,
            Allocator.TempJob
            );

        if(chunks.Length == 0) chunks.Dispose();
        else
			BufferBlockGeneration(chunks);
    }

    void BufferBlockGeneration(NativeArray<ArchetypeChunk> chunks)
    {
        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);

		for(int c = 0; c < chunks.Length; c++)
		{
			ArchetypeChunk chunk = chunks[c];

			NativeArray<Entity> 	entities    	= chunk.GetNativeArray(entityType);
            NativeArray<Position> 	positions 		= chunk.GetNativeArray(positionType);
			NativeArray<MapSquare> 	mapSquares 		= chunk.GetNativeArray(mapSquareType);
			NativeArray<AdjacentSquares> adjacent 	= chunk.GetNativeArray(adjacentType);
			
			for(int e = 0; e < entities.Length; e++)
			{
				Entity entity = entities[e];

                //  Get adjacent map square entities
				BlockBuffer(entity, positions[e], mapSquares[e], adjacent[e], commandBuffer);

				//  Set block buffer next
                commandBuffer.RemoveComponent<Tags.SetBlockBuffer>(entity);
            }
        }
    
    	commandBuffer.Playback(entityManager);
		commandBuffer.Dispose();

    	chunks.Dispose();
    }

	void BlockBuffer(Entity entity, Position position, MapSquare mapSquare, AdjacentSquares adjacent, EntityCommandBuffer commandBuffer)
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
		updateSquare.topBlockBuffer 	= topBuffer 	+ 1;
		updateSquare.bottomBlockBuffer 	= bottomBuffer 	- 1;

		//	Calculate iteration length for block generation
		int blockGenerationHeight = updateSquare.topBlockBuffer - updateSquare.bottomBlockBuffer;
		updateSquare.blockGenerationArrayLength = blockGenerationHeight * (cubeSize*cubeSize);

		//	Calculate iteration length and offset for mesh drawing
		int drawHeight = updateSquare.topDrawBuffer - updateSquare.bottomDrawBuffer;
		updateSquare.drawArrayLength = drawHeight * (cubeSize * cubeSize);
		updateSquare.drawIndexOffset = Util.Flatten(0, updateSquare.bottomDrawBuffer - updateSquare.bottomBlockBuffer, 0, cubeSize);

		//	Position of mesh in world space
		Position pos = new Position{
			Value = new float3(position.Value.x, updateSquare.bottomBlockBuffer, position.Value.z)
		};

		if(entityManager.HasComponent<BufferChange>(entity))
		{
			BufferChange updateChange = entityManager.GetComponentData<BufferChange>(entity);
			updateChange.topBlockBuffer = mapSquare.topBlockBuffer - updateSquare.topBlockBuffer;
			updateChange.bottomBlockBuffer = mapSquare.bottomBlockBuffer - updateSquare.bottomBlockBuffer;
			
			commandBuffer.SetComponent<BufferChange>(entity, updateChange);
		}

		//DEBUG
		CustomDebugTools.SetMapSquareHighlight(entity, cubeSize, new Color(1, 1, 1, 0.2f), updateSquare.topBlockBuffer, updateSquare.bottomBlockBuffer);

		commandBuffer.SetComponent<MapSquare>(entity, updateSquare);
		commandBuffer.SetComponent<Position>(entity, pos);
	}
}