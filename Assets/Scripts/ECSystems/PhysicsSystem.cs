using System;
using UnityEngine;
using Unity.Jobs;
using Unity.Entities;
using Unity.Collections;
using Unity.Transforms;
using Unity.Mathematics;
using MyComponents;

[UpdateAfter(typeof(PlayerInputSystem))]
public class PhysicsSystem : ComponentSystem
{
    EntityManager entityManager;
    int squareWidth;

    EntityArchetypeQuery moveQuery;
    EntityArchetypeQuery mapSquareQuery;

    ArchetypeChunkEntityType                    entityType;
    ArchetypeChunkComponentType<Position>       positionType;
    ArchetypeChunkComponentType<PhysicsEntity>  physicsType;

    protected override void OnCreateManager()
    {
        entityManager = World.Active.GetOrCreateManager<EntityManager>();
        squareWidth = TerrainSettings.mapSquareWidth;

        moveQuery = new EntityArchetypeQuery
        {
            Any     = Array.Empty<ComponentType>(),
            None    = Array.Empty<ComponentType>(),
            All     = new ComponentType[] { typeof(PhysicsEntity), typeof(Position) }
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
        entityType      = GetArchetypeChunkEntityType();
        positionType    = GetArchetypeChunkComponentType<Position>();
        physicsType     = GetArchetypeChunkComponentType<PhysicsEntity>();

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

            NativeArray<Entity>         entities    = chunk.GetNativeArray(entityType);
            NativeArray<Position>       positions   = chunk.GetNativeArray(positionType);
            NativeArray<PhysicsEntity>  physics     = chunk.GetNativeArray(physicsType);
            
            for(int e = 0; e < entities.Length; e++)
            {
                float3 currentPosition = positions[e].Value;
                PhysicsEntity physicsComponent = physics[e];

                float3 nextPosition = currentPosition + (physicsComponent.positionChangePerSecond * Time.deltaTime);

                //  Current map square doesn't exist, find current map Square
                if(!entityManager.Exists(physicsComponent.currentMapSquare))
                    GetMapSquare(nextPosition, out physicsComponent.currentMapSquare);

                //  Get vector describing next position's overlap from this map square
                float3 currentSquarePosition = entityManager.GetComponentData<MapSquare>(physicsComponent.currentMapSquare).position;               
                float3 overlapDirection = Util.EdgeOverlap(nextPosition - currentSquarePosition, squareWidth);

                //  Next position is outside current map square
                if(!overlapDirection.Equals(float3.zero))
                {
                    //  Get next map square from current map square's AdjacentSquares component                        
                    AdjacentSquares adjacentSquares = entityManager.GetComponentData<AdjacentSquares>(physicsComponent.currentMapSquare);
                    physicsComponent.currentMapSquare = adjacentSquares.GetByDirection(overlapDirection);
                }

                //TODO: proper physics system
                //  Get height of current block
                DynamicBuffer<Topology> heightMap = entityManager.GetBuffer<Topology>(physicsComponent.currentMapSquare);
                float3 local = Util.LocalVoxel(nextPosition, squareWidth, true);
                float yOffset = heightMap[Util.Flatten2D(local.x, local.z, squareWidth)].height;

                //  Adjust for model size
                yOffset += physics[e].size.y/2;

                positions[e] = new Position { Value = new float3(nextPosition.x, yOffset, nextPosition.z) };
                physics[e] = physicsComponent;
            }
        }
        chunks.Dispose();
    }

    //TODO: This will not be efficient for multiple moving objects
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
        throw new Exception("Could not find map square for moving object");
	}
}