using System;
using UnityEngine;
using Unity.Jobs;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Transforms;
using MyComponents;

[UpdateAfter(typeof(CubeSystem))]
public class BlockBufferSystem : ComponentSystem
{
    EntityManager entityManager;

	int cubeSize;

	EntityArchetypeQuery blockBufferQuery;

	ArchetypeChunkEntityType 				entityType;
    ArchetypeChunkComponentType<Position> 	positionType;
	ArchetypeChunkComponentType<MapSquare> 	mapSquareType;

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

			NativeArray<Entity> 	entities    = chunk.GetNativeArray(entityType);
            NativeArray<Position> 	positions 	= chunk.GetNativeArray(positionType);
			NativeArray<MapSquare> 	mapSquares 	= chunk.GetNativeArray(mapSquareType);
			
			for(int e = 0; e < entities.Length; e++)
			{
				Entity entity = entities[e];

				AdjacentSquares adjacentSquares = entityManager.GetComponentData<AdjacentSquares>(entity);

                //  Get adjacent map square entities
				BlockBuffer(entity, positions[e], mapSquares[e], adjacentSquares, commandBuffer);

				//  Set block buffer next
                commandBuffer.RemoveComponent<Tags.SetBlockBuffer>(entity);
                commandBuffer.AddComponent(entity, new Tags.GenerateBlocks());
            }
        }
    
    	commandBuffer.Playback(entityManager);
		commandBuffer.Dispose();

    	chunks.Dispose();
    }

	void BlockBuffer(Entity entity, Position position, MapSquare square, AdjacentSquares adjacent, EntityCommandBuffer commandBuffer)
	{
		MapSquare[] adjacentSquares = new MapSquare[] {
			entityManager.GetComponentData<MapSquare>(adjacent.right),
			entityManager.GetComponentData<MapSquare>(adjacent.left),
			entityManager.GetComponentData<MapSquare>(adjacent.front),
			entityManager.GetComponentData<MapSquare>(adjacent.back)
			};

		int topBuffer 		= square.topBlock;
		int bottomBuffer 	= square.bottomBlock;

		//	Set height to draw
		//int topCubeDraw 	= (int)math.floor((topBuffer + 1) / cubeSize);
		//int bottomCubeDraw 	= (int)math.floor((bottomBuffer - 1) / cubeSize);

		//	Find highest in 3x3 squares
		for(int i = 0; i < 4; i++)
		{
			int adjacentTop 	= adjacentSquares[i].topDrawBuffer;
			int adjacentBottom 	= adjacentSquares[i].bottomDrawBuffer;
			//bool outerBuffer = entityManager.HasComponent<Tags.EdgeBuffer>(adjacent[i]);
			//if(outerBuffer) continue;

			if(adjacentTop > topBuffer) topBuffer = adjacentTop;
			if(adjacentBottom < bottomBuffer) bottomBuffer = adjacentBottom;

			/*if(square.position.x == -42 && square.position.z == 672 && i == 2)
			{
				Debug.Log(adjacentBottom+" < "+square.bottomBlock);
				Debug.Log(bottomBuffer);
				CustomDebugTools.SetMapSquareHighlight(
					adjacent[i],
					cubeSize,
					Color.red,
					adjacentSquares[i].topBlock,
					adjacentSquares[i].bottomBlock);
			} */
		}

		MapSquare mapSquare = square;

		mapSquare.topBlockBuffer 	= topBuffer 	+ 1;
		mapSquare.bottomBlockBuffer = bottomBuffer 	- 1;

		int blockGenerationHeight = mapSquare.topBlockBuffer - mapSquare.bottomBlockBuffer;
		mapSquare.blockGenerationArrayLength = blockGenerationHeight * (cubeSize*cubeSize);

		int drawHeight = mapSquare.topDrawBuffer - mapSquare.bottomDrawBuffer;
		mapSquare.drawArrayLength = drawHeight * (cubeSize * cubeSize);
		mapSquare.drawIndexOffset = Util.Flatten(0, mapSquare.bottomDrawBuffer - mapSquare.bottomBlockBuffer, 0, cubeSize);

		commandBuffer.SetComponent<MapSquare>(entity, mapSquare);

		Position pos = new Position
		{
			Value = new float3(position.Value.x, mapSquare.bottomBlockBuffer, position.Value.z)
		};

		commandBuffer.SetComponent<Position>(entity, pos);
	}
}