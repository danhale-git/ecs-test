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

	EntityArchetypeQuery createCubesQuery;
	EntityArchetypeQuery allSquaresQuery;

	ArchetypeChunkEntityType entityType;
    ArchetypeChunkComponentType<Position> positionType;
	ArchetypeChunkComponentType<MapSquare> mapSquareType;

    protected override void OnCreateManager()
    {
        entityManager = World.Active.GetOrCreateManager<EntityManager>();
		cubeSize = TerrainSettings.cubeSize;

		createCubesQuery = new EntityArchetypeQuery
		{
			Any 	= Array.Empty<ComponentType>(),
            None  	= new ComponentType[] { typeof(Tags.OuterBuffer) },
			All  	= new ComponentType[] { typeof(MapSquare), typeof(Tags.CreateCubes) }
		};

        allSquaresQuery = new EntityArchetypeQuery
		{
			Any 	= Array.Empty<ComponentType>(),
			None 	= Array.Empty<ComponentType>(),
			All  	= new ComponentType[] { typeof(MapSquare) }
		};
    }

    protected override void OnUpdate()
    {
        entityType = GetArchetypeChunkEntityType();
        positionType = GetArchetypeChunkComponentType<Position>();
		mapSquareType = GetArchetypeChunkComponentType<MapSquare>();

        NativeArray<ArchetypeChunk> chunks;
        chunks = entityManager.CreateArchetypeChunkArray(
            createCubesQuery,
            Allocator.TempJob
            );

        if(chunks.Length == 0) chunks.Dispose();
        else CreateCubes(chunks);
    }

    void CreateCubes(NativeArray<ArchetypeChunk> chunks)
    {
        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);

		for(int c = 0; c < chunks.Length; c++)
		{
			ArchetypeChunk chunk = chunks[c];

			NativeArray<Entity> entities    = chunk.GetNativeArray(entityType);
            NativeArray<Position> positions = chunk.GetNativeArray(positionType);
			NativeArray<MapSquare> mapSquares = chunk.GetNativeArray(mapSquareType);
			
			for(int e = 0; e < entities.Length; e++)
			{
				Entity entity = entities[e];

                //  Get adjacent map square entities
                Entity[] adjacent;
                
                if(!GetAdjacentEntities(positions[e].Value, out adjacent))
                {
					CustomDebugTools.SetMapSquareHighlight(entity, cubeSize -1, Color.red);
					throw new System.IndexOutOfRangeException(
						"GetAdjacentBuffers did not find adjacent squares at "+positions[e].Value
						);
				}
                AdjacentSquares adjacentSquares = new AdjacentSquares{
                    right   = adjacent[0],
                    left    = adjacent[1],
                    front   = adjacent[2],
                    back    = adjacent[3],
                    };

                commandBuffer.AddComponent(entity, adjacentSquares);

                //	Create cubes and set draw height
				MapSquare square = mapSquares[e];
				CreateCubes(entity, mapSquares[e], adjacentSquares);
				mapSquares[e] = square;

                //  Generate block data next
                commandBuffer.RemoveComponent<Tags.CreateCubes>(entity);
                commandBuffer.AddComponent(entity, new Tags.SetBlocks());
            }
        }
    
    commandBuffer.Playback(entityManager);
	commandBuffer.Dispose();

    chunks.Dispose();
    }

    bool GetAdjacentEntities(float3 centerPosition, out Entity[] adjacentSquares)
	{
        adjacentSquares = new Entity[4];

		float3[] adjacentPositions = new float3[4] {
			centerPosition + (new float3( 1,  0,  0) * cubeSize),   //  right
			centerPosition + (new float3(-1,  0,  0) * cubeSize),   //  left
			centerPosition + (new float3( 0,  0,  1) * cubeSize),   //  front
			centerPosition + (new float3( 0,  0, -1) * cubeSize)    //  back
		};

		NativeArray<ArchetypeChunk> chunks = entityManager.CreateArchetypeChunkArray(
			allSquaresQuery,
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

			NativeArray<Entity> entities 	= chunk.GetNativeArray(entityType);
			NativeArray<Position> positions = chunk.GetNativeArray(positionType);

			for(int e = 0; e < positions.Length; e++)
			{
                //  check if each entity matches each position
				for(int p = 0; p < 4; p++)
				{
					float3 position = adjacentPositions[p];
					if(	position.x == positions[e].Value.x &&
						position.z == positions[e].Value.z)
					{
						adjacentSquares[p] = entities[e];
						count++;

						if(count == 4)
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

	void CreateCubes(Entity entity, MapSquare square, AdjacentSquares adjacent)
	{
	    DynamicBuffer<MapCube> cubeBuffer = entityManager.GetBuffer<MapCube>(entity);
		MapSquare[] adjacentSquares = new MapSquare[] {
			entityManager.GetComponentData<MapSquare>(adjacent.right),
			entityManager.GetComponentData<MapSquare>(adjacent.left),
			entityManager.GetComponentData<MapSquare>(adjacent.front),
			entityManager.GetComponentData<MapSquare>(adjacent.back)
			};

		int highestVisible = square.highestVisibleBlock;
		int lowestVisible = square.lowestVisibleBlock;

		//	Set height to draw
		int topCubeDraw 	= (int)math.floor((highestVisible + 1) / cubeSize);
		int bottomCubeDraw 	= (int)math.floor((lowestVisible - 1) / cubeSize);

		//	Find highest in 3x3 squares
		for(int i = 0; i < 4; i++)
		{
			int adjacentHighestVisible = adjacentSquares[i].highestVisibleBlock;
			int adjacentLowestVisible = adjacentSquares[i].lowestVisibleBlock;

			if(adjacentHighestVisible > highestVisible) highestVisible = adjacentHighestVisible;
			if(adjacentLowestVisible < lowestVisible) lowestVisible = adjacentLowestVisible;
		}

		//	Set height in cubes
		int topCubeBlocks 		= (int)math.floor((highestVisible + 1) / cubeSize) + 1;
		int bottomCubeBlocks 	= (int)math.floor((lowestVisible + 1) / cubeSize) - 1;

		for(int i = 0; i <= topCubeBlocks; i++)
		{
			int blocks = (i >= bottomCubeBlocks && i <= topCubeBlocks) ? 1 : 0;

			MapCube cube = new MapCube{
				yPos = i*cubeSize,
				blocks = blocks
				};

			cubeBuffer.Add(cube);

			//if(i == bottomCubeDraw || i == topCubeDraw)
			//	CustomDebugTools.SetMapCubeHighlight(entity, cube.yPos, cubeSize-1, new Color(1, 0, 0, 0.1f));
			//else if(i == bottomCubeBlocks || i == topCubeBlocks)
			//	CustomDebugTools.SetMapCubeHighlight(entity, cube.yPos, cubeSize-1, new Color(0, 1, 1, 0.1f));
		}
	}
}