using System;
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

	ComponentGroup adjacentGroup;

    protected override void OnCreateManager()
    {
        entityManager = World.Active.GetOrCreateManager<EntityManager>();
		cubeSize = TerrainSettings.cubeSize;

		EntityArchetypeQuery adjacentQuery = new EntityArchetypeQuery{
            None 	= new ComponentType[] { typeof(Tags.EdgeBuffer) },
			All 	= new ComponentType[] { typeof(MapSquare), typeof(Tags.GetAdjacentSquares) }
		};
		adjacentGroup = GetComponentGroup(adjacentQuery);
    }

    protected override void OnUpdate()
    {
        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);

        ArchetypeChunkEntityType 				entityType 		= GetArchetypeChunkEntityType();
        ArchetypeChunkComponentType<Position> 	positionType 	= GetArchetypeChunkComponentType<Position>();

        NativeArray<ArchetypeChunk> chunks = adjacentGroup.CreateArchetypeChunkArray(Allocator.TempJob);

		//	Map square position offsets in 8 cardinal directions
		float3[] adjacentPositions = new float3[8];
		float3[] directions = Util.CardinalDirections();
		for(int i = 0; i < directions.Length; i++)
			adjacentPositions[i] = directions[i] * cubeSize;

        for(int c = 0; c < chunks.Length; c++)
		{
			ArchetypeChunk chunk = chunks[c];

			NativeArray<Entity> 	entities    = chunk.GetNativeArray(entityType);
            NativeArray<Position> 	positions 	= chunk.GetNativeArray(positionType);
	
			for(int e = 0; e < entities.Length; e++)
			{
				Entity entity 	= entities[e];
				float3 position = positions[e].Value;

				//	Get adjacent map squares from matrix in MapManagerSystem
				AdjacentSquares adjacent = new AdjacentSquares{
					right 		= MapManagerSystem.GetMapSquareFromMatrix(position + adjacentPositions[0]),
					left 		= MapManagerSystem.GetMapSquareFromMatrix(position + adjacentPositions[1]),
					front 		= MapManagerSystem.GetMapSquareFromMatrix(position + adjacentPositions[2]),
					back 		= MapManagerSystem.GetMapSquareFromMatrix(position + adjacentPositions[3]),
					frontRight 	= MapManagerSystem.GetMapSquareFromMatrix(position + adjacentPositions[4]),
					frontLeft 	= MapManagerSystem.GetMapSquareFromMatrix(position + adjacentPositions[5]),
					backRight 	= MapManagerSystem.GetMapSquareFromMatrix(position + adjacentPositions[6]),
					backLeft 	= MapManagerSystem.GetMapSquareFromMatrix(position + adjacentPositions[7])
				};

				if(entityManager.HasComponent<AdjacentSquares>(entity))
					commandBuffer.SetComponent<AdjacentSquares>(entity, adjacent);
				else
					commandBuffer.AddComponent<AdjacentSquares>(entity, adjacent);

                commandBuffer.RemoveComponent<Tags.GetAdjacentSquares>(entity);
            }
        }
    
    	commandBuffer.Playback(entityManager);
		commandBuffer.Dispose();

    	chunks.Dispose();
    }
}