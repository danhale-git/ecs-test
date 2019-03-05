using System;
using UnityEngine;
using Unity.Jobs;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Transforms;
using MyComponents;

//	Get y buffer for mesh drawing based on adjacent top/bottom blocks
[UpdateInGroup(typeof(UpdateGroups.NewMapSquareUpdateGroup))]
public class MapAdjacentSystem : ComponentSystem
{
    EntityManager entityManager;
	MapCellMarchingSystem managerSystem;

	int squareWidth;

	ComponentGroup adjacentGroup;

    protected override void OnCreateManager()
    {
        entityManager = World.Active.GetOrCreateManager<EntityManager>();
		managerSystem = World.Active.GetOrCreateManager<MapCellMarchingSystem>();

		squareWidth = TerrainSettings.mapSquareWidth;

		EntityArchetypeQuery adjacentQuery = new EntityArchetypeQuery{
            None 	= new ComponentType[] { typeof(Tags.EdgeBuffer) },
			All 	= new ComponentType[] { typeof(MapSquare), typeof(Tags.GetAdjacentSquares) }
		};
		adjacentGroup = GetComponentGroup(adjacentQuery);
    }

    protected override void OnUpdate()
    {
        EntityCommandBuffer 		commandBuffer 	= new EntityCommandBuffer(Allocator.Temp);
        NativeArray<ArchetypeChunk> chunks 			= adjacentGroup.CreateArchetypeChunkArray(Allocator.TempJob);

        ArchetypeChunkEntityType 				entityType 		= GetArchetypeChunkEntityType();
        ArchetypeChunkComponentType<Position> 	positionType 	= GetArchetypeChunkComponentType<Position>(true);

		//	Map square position offsets in 8 cardinal directions
		float3[] adjacentPositions = new float3[8];
		float3[] directions = Util.CardinalDirections();
		for(int i = 0; i < directions.Length; i++)
			adjacentPositions[i] = directions[i] * squareWidth;

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
					right 		= managerSystem.mapMatrix.GetItem(position + adjacentPositions[0]),
					left 		= managerSystem.mapMatrix.GetItem(position + adjacentPositions[1]),
					front 		= managerSystem.mapMatrix.GetItem(position + adjacentPositions[2]),
					back 		= managerSystem.mapMatrix.GetItem(position + adjacentPositions[3]),
					frontRight 	= managerSystem.mapMatrix.GetItem(position + adjacentPositions[4]),
					frontLeft 	= managerSystem.mapMatrix.GetItem(position + adjacentPositions[5]),
					backRight 	= managerSystem.mapMatrix.GetItem(position + adjacentPositions[6]),
					backLeft 	= managerSystem.mapMatrix.GetItem(position + adjacentPositions[7])
				};

				for(int i = 0; i < 8; i++)
				{
					if(!entityManager.Exists(adjacent[i]))
					{
						CustomDebugTools.Cube(Color.red, (position + adjacentPositions[i])+(squareWidth/2), squareWidth);
		        		CustomDebugTools.Cube(Color.green, position + (squareWidth/2), squareWidth-2);
					
						Debug.Log(managerSystem.mapMatrix.GridToMatrixPosition(position));

						throw new System.Exception("Adjacent Entity does not exist at "+(position + adjacentPositions[i]));
					}
				}

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