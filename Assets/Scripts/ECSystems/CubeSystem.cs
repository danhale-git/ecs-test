using System;
using UnityEngine;
using Unity.Jobs;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Transforms;
using MyComponents;

[UpdateAfter(typeof(MapSquareSystem))]
public class CubeSystem : ComponentSystem
{
    EntityManager entityManager;
	int cubeSize;

	EntityArchetypeQuery createCubesQuery;
	EntityArchetypeQuery allSquaresQuery;

	ArchetypeChunkEntityType entityType;
    ArchetypeChunkComponentType<Position> positionType;

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
			
			for(int e = 0; e < entities.Length; e++)
			{
				Entity entity = entities[e];

                Entity[] adjacent;
                
                if(!GetAdjacentEntities(positions[e].Value, out adjacent))
                {
					CustomDebugTools.SetWireCubeChunk(positions[e].Value, cubeSize -1, Color.red);
					throw new System.IndexOutOfRangeException(
						"GetAdjacentBuffers did not find adjacent squares at "+positions[e].Value
						);
				}

                //	Create cubes
	    		DynamicBuffer<MapCube> cubeBuffer = entityManager.GetBuffer<MapCube>(entity);

    			//	TODO:   Proper cube terrain height checks and cube culling
                //          Get adjacent map squares here and store for later
    			MapCube cubePos1 = new MapCube { yPos = 0};
    			MapCube cubePos2 = new MapCube { yPos = cubeSize};
    			MapCube cubePos3 = new MapCube { yPos = cubeSize*2};
    			MapCube cubePos4 = new MapCube { yPos = cubeSize*3};

    			cubeBuffer.Add(cubePos1);
    			cubeBuffer.Add(cubePos2);
    			cubeBuffer.Add(cubePos3);
    			cubeBuffer.Add(cubePos4);

                //  Generate block data next
                commandBuffer.RemoveComponent<Tags.CreateCubes>(entity);
                commandBuffer.AddComponent(entity, new Tags.GenerateBlocks());
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
}