using System;
using UnityEngine;
using Unity.Jobs;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Transforms;
using MyComponents;

//  Generate 2D terrain data from coherent noise
[UpdateInGroup(typeof(InitializationSystemGroup))]
public class MapTopologySystem : ComponentSystem
{
    EntityManager entityManager;

    int squareWidth;

    ComponentGroup terrainGroup;

    BiomeUtility biomes;

    protected override void OnCreateManager()
    {
        entityManager   = World.Active.GetOrCreateManager<EntityManager>();
        
        squareWidth     = TerrainSettings.mapSquareWidth;

        EntityArchetypeQuery terrainQuery = new EntityArchetypeQuery{
            All     = new ComponentType[] { typeof(MapSquare), typeof(Tags.GenerateTerrain) }
        };
        terrainGroup = GetComponentGroup(terrainQuery);

        biomes = new BiomeUtility();
        biomes.InitialiseBiomes();
    }

    protected override void OnUpdate()
    {
        EntityCommandBuffer         commandBuffer   = new EntityCommandBuffer(Allocator.TempJob);
        NativeArray<ArchetypeChunk> chunks          = terrainGroup.CreateArchetypeChunkArray(Allocator.TempJob);

        ArchetypeChunkEntityType                entityType      = GetArchetypeChunkEntityType();
        ArchetypeChunkComponentType<Translation>   positionType    = GetArchetypeChunkComponentType<Translation>(true);

        for(int c = 0; c < chunks.Length; c++)
        {
            ArchetypeChunk chunk = chunks[c];

            NativeArray<Entity>     entities    = chunk.GetNativeArray(entityType);
            NativeArray<Translation>   positions   = chunk.GetNativeArray(positionType);
            
            for(int e = 0; e < entities.Length; e++)
            {
                DebugTools.IncrementDebugCount("topology");
                
                Entity entity   = entities[e];
                float3 position = positions[e].Value;

                DynamicBuffer<WorleyNoise> cellBuffer = entityManager.GetBuffer<WorleyNoise>(entity);

                TopologyJob topologyJob = new TopologyJob{
                    commandBuffer = commandBuffer,
                    position = position,
                    entity = entity,
                    cellBuffer = new NativeArray<WorleyNoise>(cellBuffer.AsNativeArray(), Allocator.TempJob),
                    squareWidth = squareWidth,
                    hasLoadedChanges = entityManager.HasComponent<LoadedChange>(entity) ? (sbyte)1 : (sbyte)0,
                    biomes = new BiomeUtility(),
                };

                topologyJob.Schedule().Complete();
            }
        }

        commandBuffer.Playback(entityManager);
        commandBuffer.Dispose();

        chunks.Dispose();
    }

    struct TopologyJob : IJob
    {
        public EntityCommandBuffer commandBuffer;

        public float3 position;
        public Entity entity;

        [DeallocateOnJobCompletion]
        public NativeArray<WorleyNoise> cellBuffer;

        public int squareWidth;
        public sbyte hasLoadedChanges;

        public BiomeUtility biomes;

        public void Execute()
        {
            int highestBlock = 0;
            int lowestBlock = 0;

            DynamicBuffer<Topology> heightBuffer = commandBuffer.SetBuffer<Topology>(entity);
			heightBuffer.ResizeUninitialized((int)math.pow(squareWidth, 2));

            for(int i = 0; i < heightBuffer.Length; i++)
            {
                int3 worldPosition = (int3)(position + Util.Unflatten2D(i, squareWidth));
                Topology heightComponent = GetHeight(cellBuffer[i], worldPosition);

                if(heightComponent.height > highestBlock)
                    highestBlock = heightComponent.height;
                if(heightComponent.height < lowestBlock)
                    lowestBlock = heightComponent.height;

                heightBuffer[i] = heightComponent;
            }

            MapSquare mapSquareComponent = new MapSquare{
                position    = new float3(position.x, 0, position.z),
                topBlock    = highestBlock,
                bottomBlock	= lowestBlock
            }; 

            //  If map square has been loaded it will already have the correct values
            if(hasLoadedChanges == 0)
                commandBuffer.SetComponent<MapSquare>(entity, mapSquareComponent);

            //  Set draw buffer next
            commandBuffer.RemoveComponent<Tags.GenerateTerrain>(entity);
        }

        Topology GetHeight(WorleyNoise cell, int3 worldPosition)
        {
            float height = TerrainSettings.terrainHeight;
            int levelHeight = TerrainSettings.levelHeight;
            float cliffDepth = TerrainSettings.cliffDepth;
            int levelCount = TerrainSettings.levelCount;
            TerrainTypes type = 0;

            float cellValue = cell.currentCellValue;
            float adjacentValue = cell.adjacentCellValue;

            float increment = 1.0f / levelCount;

            float cellHeight = math.lerp(0, levelCount, cellValue) * levelHeight;
            float adjacentHeight = math.lerp(0, levelCount, adjacentValue) * levelHeight;

            cellHeight += biomes.AddNoise(cell.currentCellValue, worldPosition.x, worldPosition.z);
            adjacentHeight += biomes.AddNoise(cell.adjacentCellValue, worldPosition.x, worldPosition.z);

            //  Close to the edge between two cells of different heights = cliff
            if(cell.distance2Edge < cliffDepth*2 && cellHeight != adjacentHeight)
            {
                type = TerrainTypes.CLIFF;            
            
                //  Closer to the edge between cells, interpolate
                //  between cell heigts for smooth transition
                if(cell.distance2Edge < cliffDepth) 
                {
                    float halfway = (cellHeight + adjacentHeight) / 2;
                    float interpolator = math.unlerp(0, cliffDepth, cell.distance2Edge);

                    //  Interpolate towards midpoint using distance from midpoint
                    height += math.lerp(halfway, cellHeight, interpolator);
                }
                else
                {
                    height += cellHeight;
                }

                float cliffDetailInterp = math.unlerp(cliffDepth*2, 0, cell.distance2Edge);
                float cliffDetail = math.lerp(0, biomes.CliffDetail(worldPosition.x, worldPosition.z), cliffDetailInterp);
                height += cliffDetail;
            }
            //  If not cliff then grass
            else
            {

                type = TerrainTypes.GRASS;
                height += cellHeight;
            }


            return new Topology{
                height = (int)height,
                type = type
            };
        }
    }
}