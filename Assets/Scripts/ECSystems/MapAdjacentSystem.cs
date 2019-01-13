﻿using System;
using UnityEngine;
using Unity.Jobs;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Transforms;
using MyComponents;

//	Get y buffer for mesh drawing based on adjacent top/bottom blocks
[UpdateAfter(typeof(MapTopologySystem))]
public class MapAdjacentSystem : ComponentSystem
{
    EntityManager entityManager;

	int cubeSize;

	EntityArchetypeQuery adjacentQuery;
	EntityArchetypeQuery allMapSquaresQuery;

	ArchetypeChunkEntityType 				entityType;
    ArchetypeChunkComponentType<Position> 	positionType;
	ArchetypeChunkComponentType<MapSquare> 	mapSquareType;

    protected override void OnCreateManager()
    {
        entityManager = World.Active.GetOrCreateManager<EntityManager>();
		cubeSize = TerrainSettings.cubeSize;

		adjacentQuery = new EntityArchetypeQuery
		{
			Any 	= Array.Empty<ComponentType>(),
            None  	= new ComponentType[] { typeof(Tags.EdgeBuffer) },
			All  	= new ComponentType[] { typeof(MapSquare), typeof(Tags.GetAdjacentSquares) }
		};

        allMapSquaresQuery = new EntityArchetypeQuery
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
            adjacentQuery,
            Allocator.TempJob
            );

        if(chunks.Length == 0) chunks.Dispose();
        else
			GetAdjacentSquares(chunks);
    }

    void GetAdjacentSquares(NativeArray<ArchetypeChunk> chunks)
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

				//  Set block buffer next
                commandBuffer.RemoveComponent<Tags.GetAdjacentSquares>(entity);

                commandBuffer.AddComponent(entity, new Tags.SetDrawBuffer());
            }
        }
    
    	commandBuffer.Playback(entityManager);
		commandBuffer.Dispose();

    	chunks.Dispose();
    }

    bool GetAdjacentEntities(float3 centerPosition, out Entity[] adjacentSquares)
	{
        adjacentSquares = new Entity[8];

		float3[] adjacentPositions = new float3[8];
		float3[] directions = Util.CardinalDirections();
		for(int i = 0; i < directions.Length; i++)
			adjacentPositions[i] = centerPosition + (directions[i] * cubeSize);

		NativeArray<ArchetypeChunk> chunks = entityManager.CreateArchetypeChunkArray(
			allMapSquaresQuery,
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