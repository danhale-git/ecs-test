using System;
using UnityEngine;
using Unity.Jobs;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Transforms;
using MyComponents;

//  Generate 2D terrain data from coherent noise
[UpdateInGroup(typeof(UpdateGroups.NewMapSquareUpdateGroup))]
public class MapTopologySystem : ComponentSystem
{
    EntityManager entityManager;

    int squareWidth;

    int levelHeight = 5;
    float cliffDepth = 0.05f;
    int levelCount = 5;

    ComponentGroup terrainGroup;

    JobifiedNoise jobifiedNoise;

    protected override void OnCreateManager()
    {
        entityManager   = World.Active.GetOrCreateManager<EntityManager>();
        
        squareWidth     = TerrainSettings.mapSquareWidth;

        EntityArchetypeQuery terrainQuery = new EntityArchetypeQuery{
            All     = new ComponentType[] { typeof(MapSquare), typeof(Tags.GenerateTerrain) }
        };
        terrainGroup = GetComponentGroup(terrainQuery);

        jobifiedNoise = new JobifiedNoise();
    }

    protected override void OnUpdate()
    {
        EntityCommandBuffer         commandBuffer   = new EntityCommandBuffer(Allocator.Temp);
        NativeArray<ArchetypeChunk> chunks          = terrainGroup.CreateArchetypeChunkArray(Allocator.TempJob);

        ArchetypeChunkEntityType                entityType      = GetArchetypeChunkEntityType();
        ArchetypeChunkComponentType<Position>   positionType    = GetArchetypeChunkComponentType<Position>(true);

        for(int c = 0; c < chunks.Length; c++)
        {
            ArchetypeChunk chunk = chunks[c];

            NativeArray<Entity>     entities    = chunk.GetNativeArray(entityType);
            NativeArray<Position>   positions   = chunk.GetNativeArray(positionType);
            
            for(int e = 0; e < entities.Length; e++)
            {
                Entity entity   = entities[e];
                float3 position = positions[e].Value;

                DynamicBuffer<CellProfile> cellBuffer = entityManager.GetBuffer<CellProfile>(entity);
			    cellBuffer.ResizeUninitialized((int)math.pow(squareWidth, 2));

                DynamicBuffer<Topology> heightBuffer = entityManager.GetBuffer<Topology>(entity);
			    heightBuffer.ResizeUninitialized((int)math.pow(squareWidth, 2));

                NativeArray<CellProfile> cellMap = jobifiedNoise.CellularDistanceToEdge(position, TerrainSettings.cellFrequency);

                for(int i = 0; i < cellBuffer.Length; i++)
                    cellBuffer[i] = cellMap[i];

                int highestBlock = 0;
                int lowestBlock = 0;

                for(int i = 0; i < heightBuffer.Length; i++)
                {
                    Topology heightComponent = GetCellHeight(cellMap[i]);

                    if(heightComponent.height > highestBlock)
                        highestBlock = heightComponent.height;
                    if(heightComponent.height < lowestBlock)
                        lowestBlock = heightComponent.height;

                    heightBuffer[i] = heightComponent;
                }

                MapSquare mapSquareComponent = new MapSquare{
                    position = new float3(position.x, 0, position.z),
                    topBlock    = highestBlock,
                    bottomBlock	= lowestBlock
                    }; 

                //  If map square has been loaded it will already have the correct values
                if(!entityManager.HasComponent<LoadedChange>(entity))
			        entityManager.SetComponentData<MapSquare>(entity, mapSquareComponent);

                //  Set draw buffer next
                commandBuffer.RemoveComponent<Tags.GenerateTerrain>(entity);

                cellMap.Dispose();
            }
        }

        commandBuffer.Playback(entityManager);
        commandBuffer.Dispose();

        chunks.Dispose();
    }

    Topology GetCellHeight(CellProfile cell)
    {
        int height = TerrainSettings.terrainHeight;
        TerrainTypes type = 0;

        float cellValue = cell.currentCellValue;
        float adjacentValue = cell.adjacentCellValue;

        float increment = 1.0f / levelCount;

        float cellHeight = math.lerp(0, levelCount, cellValue) * levelHeight;
        float adjacentHeight = math.lerp(0, levelCount, adjacentValue) * levelHeight;
        
        //  Close to the edge between two cells of different heights = cliff
        if(cell.distance2Edge < cliffDepth*2 && cellHeight != adjacentHeight)
        {
            type = TerrainTypes.CLIFF;            
        
            //  Closer to the edge between cells, interpolate
            //  between cell heigts for smooth transition
            if(cell.distance2Edge < cliffDepth) 
            {
                float halfway = (cellHeight + adjacentHeight) / 2;
                float interpolator = Mathf.InverseLerp(0, cliffDepth, cell.distance2Edge);

                //  Interpolate towards midpoint using distance from midpoint
                height += (int)math.lerp(halfway, cellHeight, interpolator);
            }
            else
                height += (int)cellHeight;
        }
        //  If not cliff then grass
        else
        {
            type = TerrainTypes.GRASS;
            height += (int)cellHeight;
        }

        return new Topology{
            height = height,
            type = type
        };
    }
}