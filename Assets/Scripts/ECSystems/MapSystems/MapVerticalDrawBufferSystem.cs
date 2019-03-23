using Unity.Jobs;
using Unity.Entities;
using Unity.Collections;
using MyComponents;

[UpdateAfter(typeof(InitializationSystemGroup))]
public class MapVerticalDrawBoundsSystem : ComponentSystem
{
    EntityManager entityManager;

	int squareWidth;

	ComponentGroup drawBufferGroup;
	ComponentGroup blockBufferGroup;    

    protected override void OnCreateManager()
    {
        entityManager = World.Active.GetOrCreateManager<EntityManager>();
		
		squareWidth = TerrainSettings.mapSquareWidth;

		EntityArchetypeQuery drawBufferQuery = new EntityArchetypeQuery{
            None 	= new ComponentType[] { typeof(Tags.EdgeBuffer) },
			All 	= new ComponentType[] { typeof(MapSquare), typeof(Tags.SetVerticalDrawBounds), typeof(AdjacentSquares) }
		};
		drawBufferGroup = GetComponentGroup(drawBufferQuery);

        EntityArchetypeQuery blockBufferQuery = new EntityArchetypeQuery{
            None  	= new ComponentType[] { typeof(Tags.EdgeBuffer), typeof(Tags.OuterBuffer) },
			All  	= new ComponentType[] { typeof(MapSquare), typeof(Tags.SetVerticalDrawBounds), typeof(AdjacentSquares) }
		};
		blockBufferGroup = GetComponentGroup(blockBufferQuery);
    }

    protected override void OnUpdate()
    {
        UpdateDrawBounds();
        UpdateBlockBuffer();
    }

    void UpdateDrawBounds()
    {
        EntityCommandBuffer 		commandBuffer 	= new EntityCommandBuffer(Allocator.Temp);
        NativeArray<ArchetypeChunk> chunks 			= drawBufferGroup.CreateArchetypeChunkArray(Allocator.TempJob);

		ArchetypeChunkEntityType 						entityType 		= GetArchetypeChunkEntityType();
		ArchetypeChunkComponentType<MapSquare> 			mapSquareType 	= GetArchetypeChunkComponentType<MapSquare>(true);
		ArchetypeChunkComponentType<AdjacentSquares> 	adjacentType 	= GetArchetypeChunkComponentType<AdjacentSquares>(true);

		for(int c = 0; c < chunks.Length; c++)
		{
			ArchetypeChunk chunk = chunks[c];

			NativeArray<Entity> 			entities    = chunk.GetNativeArray(entityType);
			NativeArray<MapSquare> 			mapSquares 	= chunk.GetNativeArray(mapSquareType);
			NativeArray<AdjacentSquares> 	adjacent 	= chunk.GetNativeArray(adjacentType);
			
			for(int e = 0; e < entities.Length; e++)
			{
            	DebugTools.IncrementDebugCount("Vbuffer");

				Entity 			entity 			= entities[e];
				AdjacentSquares adjacentSquares = adjacent[e];

                //	Check top and bottom limits for drawing map square
				DrawBuffer(entity, mapSquares[e], adjacentSquares, commandBuffer);
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

    void UpdateBlockBuffer()
    {
        EntityCommandBuffer 		commandBuffer 	= new EntityCommandBuffer(Allocator.Temp);
        NativeArray<ArchetypeChunk> chunks 			= blockBufferGroup.CreateArchetypeChunkArray(Allocator.TempJob);

        ArchetypeChunkEntityType 						entityType 		= GetArchetypeChunkEntityType();
		ArchetypeChunkComponentType<MapSquare> 			mapSquareType 	= GetArchetypeChunkComponentType<MapSquare>(true);
		ArchetypeChunkComponentType<AdjacentSquares> 	adjacentType 	= GetArchetypeChunkComponentType<AdjacentSquares>(true);

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
                commandBuffer.RemoveComponent<Tags.SetVerticalDrawBounds>(entity);
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
			int adjacentTop 	= adjacentSquare.topDrawBounds;
			int adjacentBottom 	= adjacentSquare.bottomDrawBounds;

			if(adjacentTop > topBuffer) topBuffer = adjacentTop;
			if(adjacentBottom < bottomBuffer) bottomBuffer = adjacentBottom;
		}

		MapSquare updateSquare = mapSquare;

		//	Top and bottom block levels to generate blocks
		updateSquare.topBlockBuffer 	= topBuffer 	+ 2;
		updateSquare.bottomBlockBuffer 	= bottomBuffer 	- 1;

		//	Calculate iteration length for block generation
		int blockGenerationHeight = (updateSquare.topBlockBuffer - updateSquare.bottomBlockBuffer)+1;
		updateSquare.blockDataArrayLength = blockGenerationHeight * (squareWidth*squareWidth);

		//	Calculate iteration length and offset for mesh drawing
		int drawHeight = (updateSquare.topDrawBounds - updateSquare.bottomDrawBounds)+1;
		updateSquare.blockDrawArrayLength = drawHeight * (squareWidth * squareWidth);
		updateSquare.drawIndexOffset = Util.Flatten(0, updateSquare.bottomDrawBounds - updateSquare.bottomBlockBuffer, 0, squareWidth);
		
		commandBuffer.SetComponent<MapSquare>(entity, updateSquare);
	}
}
