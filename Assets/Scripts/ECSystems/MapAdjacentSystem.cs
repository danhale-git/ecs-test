using System;
using System.Collections.Generic;//DEBUG
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

			GetAdjacentEntities(entities, positions, commandBuffer);
			
			for(int e = 0; e < entities.Length; e++)
			{
				Entity entity = entities[e];

                commandBuffer.RemoveComponent<Tags.GetAdjacentSquares>(entity);
            }
        }
    
    	commandBuffer.Playback(entityManager);
		commandBuffer.Dispose();

    	chunks.Dispose();
    }

    void GetAdjacentEntities(NativeArray<Entity> centerEntities, NativeArray<Position> centerPositions, EntityCommandBuffer commandBuffer)
	{
		Entity[][] adjacentSquares = new Entity[centerEntities.Length][];

		for(int c = 0; c < centerEntities.Length; c++)
			adjacentSquares[c] = new Entity[8];

		float3[] adjacentPositions = new float3[8];
		float3[] directions = Util.CardinalDirections();
		for(int i = 0; i < directions.Length; i++)
			adjacentPositions[i] = directions[i] * cubeSize;

		NativeArray<ArchetypeChunk> chunks = entityManager.CreateArchetypeChunkArray(
			allMapSquaresQuery,
			Allocator.TempJob
		);

		for(int d = 0; d < chunks.Length; d++)
		{
			ArchetypeChunk chunk = chunks[d];

			NativeArray<Entity>   adjacentEntities 	= chunk.GetNativeArray(entityType);
			NativeArray<Position> adjacentEntityPositions = chunk.GetNativeArray(positionType);

			for(int e = 0; e < adjacentEntities.Length; e++)
			{
				for(int c = 0; c < centerEntities.Length; c++)
				{
					//  check if each entity matches each position
					for(int p = 0; p < adjacentPositions.Length; p++)
					{
						float3 position = adjacentPositions[p] + centerPositions[c].Value;
						
						if(	position.x == adjacentEntityPositions[e].Value.x &&
							position.z == adjacentEntityPositions[e].Value.z)
						{
							adjacentSquares[c][p] = adjacentEntities[e];
						}
					}
				}
			}
		}

		for(int i = 0; i < adjacentSquares.Length; i++)
		{
			AdjacentSquares adjacent = new AdjacentSquares{
				right 		= adjacentSquares[i][0],
				left 		= adjacentSquares[i][1],
				front 		= adjacentSquares[i][2],
				back 		= adjacentSquares[i][3],
				frontRight 	= adjacentSquares[i][4],
				frontLeft 	= adjacentSquares[i][5],
				backRight 	= adjacentSquares[i][6],
				backLeft 	= adjacentSquares[i][7]
			};

			commandBuffer.AddComponent(centerEntities[i], adjacent);
		}

		chunks.Dispose();
		return;
	}
}