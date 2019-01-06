using System;
using UnityEngine;
using Unity.Jobs;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Transforms;
using MyComponents;

//	Get y buffer for mesh drawing based on adjacent top/bottom blocks
[UpdateAfter(typeof(TerrainSystem))]
public class DrawBufferSystem : ComponentSystem
{
    EntityManager entityManager;

	int cubeSize;

	EntityArchetypeQuery drawBufferQuery;
	EntityArchetypeQuery getAdjacentEntitiesQuery;

	ArchetypeChunkEntityType 				entityType;
    ArchetypeChunkComponentType<Position> 	positionType;
	ArchetypeChunkComponentType<MapSquare> 	mapSquareType;

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

        getAdjacentEntitiesQuery = new EntityArchetypeQuery
		{
			Any 	= Array.Empty<ComponentType>(),
			None 	= Array.Empty<ComponentType>(),
			All  	= new ComponentType[] { typeof(MapSquare) }
		};
    }

    protected override void OnUpdate()
    {
        entityType 		= GetArchetypeChunkEntityType();
        positionType 	= GetArchetypeChunkComponentType<Position>();
		mapSquareType 	= GetArchetypeChunkComponentType<MapSquare>();

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

			NativeArray<Entity> 	entities    = chunk.GetNativeArray(entityType);
            NativeArray<Position> 	positions 	= chunk.GetNativeArray(positionType);
			NativeArray<MapSquare> 	mapSquares 	= chunk.GetNativeArray(mapSquareType);
			
			for(int e = 0; e < entities.Length; e++)
			{
				Entity entity = entities[e];

                Entity[] adjacent;
                
				//	Get 8 adjacent map squares
                if(!GetAdjacentEntities(positions[e].Value, out adjacent))
                {
					//CustomDebugTools.SetMapSquareHighlight(entity, cubeSize -1, Color.red);
					throw new System.IndexOutOfRangeException(
						"GetAdjacentBuffers did not find adjacent squares at "+positions[e].Value
						);
				}

                AdjacentSquares adjacentSquares = new AdjacentSquares{
                    right   	= adjacent[0],
                    left    	= adjacent[1],
                    front   	= adjacent[2],
                    back    	= adjacent[3],
					frontRight	= adjacent[4],
					frontLeft	= adjacent[5],
					backRight	= adjacent[6],
					backLeft	= adjacent[7]
                    };

                commandBuffer.AddComponent(entity, adjacentSquares);

                //	Check top and bottom limits for drawing map square
				DrawBuffer(entity, mapSquares[e], adjacentSquares, commandBuffer);

				//  Set block buffer next
                commandBuffer.RemoveComponent<Tags.SetDrawBuffer>(entity);
                commandBuffer.AddComponent(entity, new Tags.SetBlockBuffer());
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

		commandBuffer.SetComponent<MapSquare>(entity, updateSquare);
	}

    bool GetAdjacentEntities(float3 centerPosition, out Entity[] adjacentSquares)
	{
        adjacentSquares = new Entity[8];

		float3[] adjacentPositions = new float3[8];
		float3[] directions = Util.CardinalDirections();
		for(int i = 0; i < directions.Length; i++)
			adjacentPositions[i] = centerPosition + (directions[i] * cubeSize);

		NativeArray<ArchetypeChunk> chunks = entityManager.CreateArchetypeChunkArray(
			getAdjacentEntitiesQuery,
			Allocator.TempJob
		);

		if(chunks.Length == 0)
		{
			chunks.Dispose();
			return false;
		}

		int count = 0;
		for(int d = 0; d < chunks.Length; d++)
		{
			ArchetypeChunk chunk = chunks[d];

			NativeArray<Entity>   entities 	= chunk.GetNativeArray(entityType);
			NativeArray<Position> positions = chunk.GetNativeArray(positionType);

			for(int e = 0; e < positions.Length; e++)
			{
                //  check if each entity matches each position
				for(int p = 0; p < adjacentPositions.Length; p++)
				{
					float3 position = adjacentPositions[p];
					
					if(	position.x == positions[e].Value.x &&
						position.z == positions[e].Value.z)
					{
						adjacentSquares[p] = entities[e];
						count++;

						if(count == adjacentPositions.Length)
						{
							chunks.Dispose();
							return true;
						}
					}
				}
			}
		}

		chunks.Dispose();
		return false;
	}
}