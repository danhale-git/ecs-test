using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Transforms;
using System.Collections.Generic;
using MyComponents;

[UpdateAfter(typeof(MapManagerSystem))]
public class MapCellSystem : ComponentSystem
{
    EntityManager entityManager;

    int squareWidth;

    ComponentGroup group;

    JobifiedNoise jobifiedNoise;

    public Dictionary<float, Entity> cells; 

    protected override void OnCreateManager()
    {
        entityManager = World.Active.GetOrCreateManager<EntityManager>();

        squareWidth = TerrainSettings.mapSquareWidth;

        EntityArchetypeQuery query = new EntityArchetypeQuery{
            All = new ComponentType[] { typeof(Tags.GenerateCells) }
        };
        group = GetComponentGroup(query);

        jobifiedNoise = new JobifiedNoise();
    }

    protected override void OnUpdate()
    {
        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);

        ArchetypeChunkEntityType                entityType      = GetArchetypeChunkEntityType();
        ArchetypeChunkComponentType<Position>   positionType    = GetArchetypeChunkComponentType<Position>(true);

        NativeArray<ArchetypeChunk> chunks = group.CreateArchetypeChunkArray(Allocator.TempJob);

        for(int c = 0; c < chunks.Length; c++)
        {
            NativeArray<Entity>     entities    = chunks[c].GetNativeArray(entityType);
            NativeArray<Position>   positions   = chunks[c].GetNativeArray(positionType);

            for(int e = 0; e < entities.Length; e++)
            {
                Entity entity   = entities[e];
                float3 position = positions[e].Value;

                DynamicBuffer<CellProfile> cellBuffer = entityManager.GetBuffer<CellProfile>(entity);
			    cellBuffer.ResizeUninitialized(0);

                NativeArray<CellProfile> cellMap = jobifiedNoise.CellularDistanceToEdge(position, TerrainSettings.cellFrequency);

                cellBuffer.AddRange(cellMap);
                cellMap.Dispose();

                Dictionary<float, int> cellCounts = new Dictionary<float, int>();

                float previousCellValue = 0;

                for(int i = 0; i < cellBuffer.Length; i++)
                {
                    float currentCellValue = cellBuffer[i].currentCellValue;

                    if(currentCellValue == previousCellValue)
                    {
                        continue;
                    }

                    int currentCount;

                    if(cellCounts.TryGetValue(currentCellValue, out currentCount))
                        cellCounts[currentCellValue] = currentCount+1;
                    else
                        cellCounts.Add(currentCellValue, 1);

                    previousCellValue = currentCellValue;                                    
                }

                commandBuffer.RemoveComponent<Tags.GenerateCells>(entity);
            }
        }

        commandBuffer.Playback(entityManager);
        commandBuffer.Dispose();

        chunks.Dispose();
    }

    void NewCell(float cellValue)
    {
        if(cells.ContainsKey(cellValue)) return;

        //  Create cell entity
    }
}