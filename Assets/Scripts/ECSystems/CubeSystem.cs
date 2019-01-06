using System;
using UnityEngine;
using Unity.Jobs;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Transforms;
using MyComponents;

[UpdateAfter(typeof(TerrainSystem))]
public class CubeSystem : ComponentSystem
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
            None  	= new ComponentType[] { typeof(Tags.OuterBuffer) },
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

                //  Get adjacent map square entities
                Entity[] adjacent;
                
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

                //	Create cubes
				MapSquare square 	= mapSquares[e];
				DrawBuffer(entity, positions[e], mapSquares[e], adjacentSquares, commandBuffer);
				mapSquares[e] 		= square;

				//  Set block buffer next
                commandBuffer.RemoveComponent<Tags.SetDrawBuffer>(entity);
                commandBuffer.AddComponent(entity, new Tags.SetBlockBuffer());
            }
        }
    
    	commandBuffer.Playback(entityManager);
		commandBuffer.Dispose();

    	chunks.Dispose();
    }

	void DrawBuffer(Entity entity, Position position, MapSquare square, AdjacentSquares adjacent, EntityCommandBuffer commandBuffer)
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
			int adjacentTop 	= adjacentSquares[i].topBlock;
			int adjacentBottom 	= adjacentSquares[i].bottomBlock;

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

		mapSquare.topDrawBuffer		= topBuffer + 1;
		mapSquare.bottomDrawBuffer	= bottomBuffer - 1;

		commandBuffer.SetComponent<MapSquare>(entity, mapSquare);
	}

    bool GetAdjacentEntities(float3 centerPosition, out Entity[] adjacentSquares)
	{
        adjacentSquares = new Entity[8];

		float3[] adjacentPositions = new float3[8];
		float3[] directions = Util.CardinalDirections();
		for(int i = 0; i < directions.Length; i++)
			adjacentPositions[i] = centerPosition + (directions[i] * cubeSize);

		/*float3[] adjacentPositions = new float3[8] {
			centerPosition + (new float3( 1,  0,  0) * cubeSize),   //  right
			centerPosition + (new float3(-1,  0,  0) * cubeSize),   //  left
			centerPosition + (new float3( 0,  0,  1) * cubeSize),   //  front
			centerPosition + (new float3( 0,  0, -1) * cubeSize),   //  back
			centerPosition + (new float3( 1,  0,  1) * cubeSize),   //  front right
			centerPosition + (new float3(-1,  0,  1) * cubeSize),   //  front left
			centerPosition + (new float3( 1,  0, -1) * cubeSize),   //  back right
			centerPosition + (new float3(-1,  0, -1) * cubeSize)	//  back left
		};*/

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