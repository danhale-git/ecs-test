using System;
using UnityEngine;
using Unity.Jobs;
using Unity.Entities;
using Unity.Collections;
using Unity.Transforms;
using Unity.Mathematics;
using MyComponents;

[UpdateAfter(typeof(PlayerInputSystem))]
public class MoveSystem : ComponentSystem
{
    EntityManager entityManager;
    int cubeSize;

    EntityArchetypeQuery moveQuery;
    EntityArchetypeQuery mapSquareQuery;

    ArchetypeChunkEntityType entityType;
    ArchetypeChunkComponentType<Position> positionType;
    ArchetypeChunkComponentType<Movement> moveType;

    protected override void OnCreateManager()
    {
        entityManager = World.Active.GetOrCreateManager<EntityManager>();
        cubeSize = TerrainSettings.cubeSize;

        moveQuery = new EntityArchetypeQuery{
            Any     = Array.Empty<ComponentType>(),
            None    = Array.Empty<ComponentType>(),
            All     = new ComponentType[] { typeof(Movement), typeof(Position) }
        };

        //	All map squares
		mapSquareQuery = new EntityArchetypeQuery{		
			Any 	= System.Array.Empty<ComponentType>(),
			None 	= new ComponentType [] { typeof(Tags.InnerBuffer), typeof(Tags.OuterBuffer), typeof(Tags.EdgeBuffer) },
			All 	= new ComponentType [] { typeof(MapSquare) }
			};
    }

    protected override void OnUpdate()
    {
        entityType = GetArchetypeChunkEntityType();
        positionType = GetArchetypeChunkComponentType<Position>();
        moveType = GetArchetypeChunkComponentType<Movement>();

        NativeArray<ArchetypeChunk> chunks;
        chunks = entityManager.CreateArchetypeChunkArray(
            moveQuery,
            Allocator.TempJob
        );

        if(chunks.Length == 0) chunks.Dispose();
        else MoveEntities(chunks);
    }

    void MoveEntities(NativeArray<ArchetypeChunk> chunks)
    {
        for(int c = 0; c < chunks.Length; c++)
        {
            ArchetypeChunk chunk = chunks[c];

            NativeArray<Entity> entities = chunk.GetNativeArray(entityType);
            NativeArray<Position> positions = chunk.GetNativeArray(positionType);
            NativeArray<Movement> movements = chunk.GetNativeArray(moveType);
            
            for(int e = 0; e < entities.Length; e++)
            {
                float3 currentPosition = positions[e].Value;
                Movement movement = movements[e];

                float3 nextPosition = currentPosition + (movements[e].positionChangePerSecond * Time.deltaTime);

                float yOffset = nextPosition.y;

                //  Current map square doesn't exist, find current map Square
                if(!entityManager.Exists(movements[e].currentMapSquare))
                {
                    Entity mapSquare;
                    if(!GetMapSquare(nextPosition, out mapSquare))
                        throw new Exception("Could not find map square for moving object");
                    else
                        movement.currentMapSquare = mapSquare;
                }
                //  Check if current map square changes after this movement
                else
                {
                    float3 currentSquarePosition = entityManager.GetComponentData<MapSquare>(movements[e].currentMapSquare).position;
                    float3 overlapDirection = Util.EdgeOverlap(nextPosition - currentSquarePosition, cubeSize);

                    //  Get next map square from current map square's AdjacentSquares component
                    if(!Util.Float3sMatch(overlapDirection, float3.zero))
                    {
                        AdjacentSquares adjacentSquares = entityManager.GetComponentData<AdjacentSquares>(movements[e].currentMapSquare);
                        movement.currentMapSquare = adjacentSquares.GetByDirection(overlapDirection);
                    }
                }

                DynamicBuffer<Topology> heightMap = entityManager.GetBuffer<Topology>(movement.currentMapSquare);
                float3 local = Util.LocalPosition(nextPosition, cubeSize);
                yOffset = heightMap[Util.Flatten2D(local.x, local.z, cubeSize)].height;

                yOffset += movements[e].size.y/2;

                positions[e] = new Position { Value = new float3(nextPosition.x, yOffset, nextPosition.z) };
                movements[e] = movement;
            }
        }
        chunks.Dispose();
    }

    // TODO: This will not be efficient for multiple moving objects
    //	Get map square by position using chunk iteration
	bool GetMapSquare(float3 position, out Entity mapSquare)
	{
		entityType	 	= GetArchetypeChunkEntityType();
		positionType	= GetArchetypeChunkComponentType<Position>();

		//	All map squares
		NativeArray<ArchetypeChunk> chunks = entityManager.CreateArchetypeChunkArray(
			mapSquareQuery,
			Allocator.TempJob
			);

		if(chunks.Length == 0)
		{
			chunks.Dispose();
			mapSquare = new Entity();
			return false;
		}

		for(int d = 0; d < chunks.Length; d++)
		{
			ArchetypeChunk chunk = chunks[d];

			NativeArray<Entity> entities 	= chunk.GetNativeArray(entityType);
			NativeArray<Position> positions = chunk.GetNativeArray(positionType);

			for(int e = 0; e < entities.Length; e++)
			{
				Entity entity = entities[e];

				//	If position matches return
				if(	position.x == positions[e].Value.x &&
					position.z == positions[e].Value.z)
				{
					mapSquare = entity;
					chunks.Dispose();
					return true;
				}
			}
		}

		chunks.Dispose();
		mapSquare = new Entity();
		return false;
	}
}