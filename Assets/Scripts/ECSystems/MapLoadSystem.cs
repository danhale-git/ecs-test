using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Transforms;
using MyComponents;

[UpdateAfter(typeof(MapSaveSystem))]
public class MapLoadSystem : ComponentSystem
{
    EntityManager entityManager;
    MapSaveSystem mapSaveSystem;

    int cubeSize;

    ComponentGroup loadGroup;

    protected override void OnCreateManager()
    {
        entityManager = World.Active.GetOrCreateManager<EntityManager>();
		mapSaveSystem = World.Active.GetOrCreateManager<MapSaveSystem>();

        cubeSize = TerrainSettings.cubeSize;

        EntityArchetypeQuery loadQuery = new EntityArchetypeQuery{
            None = new ComponentType[] { },
            All = new ComponentType[] { typeof(MapSquare), typeof(Tags.LoadChanges) }
        };
        loadGroup = GetComponentGroup(loadQuery);
    }

    protected override void OnUpdate()
    {
        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);

        ArchetypeChunkEntityType entityType = GetArchetypeChunkEntityType();
        ArchetypeChunkComponentType<Position> positionType = GetArchetypeChunkComponentType<Position>();

        NativeArray<ArchetypeChunk> chunks = loadGroup.CreateArchetypeChunkArray(Allocator.TempJob);

        for(int c = 0; c < chunks.Length; c++)
        {
            NativeArray<Entity>     entities    = chunks[c].GetNativeArray(entityType);
            NativeArray<Position>   positions   = chunks[c].GetNativeArray(positionType);

            for(int e = 0; e < entities.Length; e++)
            {
                Entity entity = entities[e];

                MapSaveSystem.SaveData data;
                if(LoadMapSquareChanges(positions[e].Value, out data))
                {
                    Debug.Log("found saved change");
                    CustomDebugTools.MarkError(entity, Color.green);

                    ApplyChanges(entities[e], data, commandBuffer);
                }

                commandBuffer.RemoveComponent<Tags.LoadChanges>(entity);
            }
        }

        commandBuffer.Playback(entityManager);
        commandBuffer.Dispose();

        chunks.Dispose();
    }

    void ApplyChanges(Entity entity, MapSaveSystem.SaveData data, EntityCommandBuffer commandBuffer)
    {
        DynamicBuffer<LoadedChange> appliedChanges = GetOrCreateLoadedChangeBuffer(entity, commandBuffer);

        //  Apply saved block changes
        for(int i = 0; i < data.changes.Length; i++)
            appliedChanges.Add(new LoadedChange { block = data.changes[i] });

        //  Apply saved map square
        commandBuffer.SetComponent<MapSquare>(entity, data.mapSquare);

        commandBuffer.RemoveComponent<Tags.SetDrawBuffer>(entity);
        commandBuffer.RemoveComponent<Tags.SetBlockBuffer>(entity);
    }

    bool LoadMapSquareChanges(float3 squarePosition, out MapSaveSystem.SaveData data)
    {
        data = new MapSaveSystem.SaveData();
         //  Index of map square in acre matrix
        float3  acrePosition            = mapSaveSystem.AcreRootPosition(squarePosition);
        float3  mapSquareMatrixIndex    = (squarePosition - acrePosition) / cubeSize;
        int     mapSquareIndex          = Util.Flatten2D(mapSquareMatrixIndex.x, mapSquareMatrixIndex.z, MapSaveSystem.acreSize);

        MapSaveSystem.SaveData[] acre;

        //  Acre does not exist
        if(!mapSaveSystem.allAcres.TryGetValue(acrePosition, out acre))
            return false;

        //  Map square has no changes
        if(!mapSaveSystem.mapSquareChanged[acrePosition][mapSquareIndex])
            return false;

        Debug.Log("loading from: "+ acrePosition+" with index: "+mapSquareIndex);        

        //  Map square has changes
        data = acre[mapSquareIndex];
        return true;
    }

    DynamicBuffer<LoadedChange> GetOrCreateLoadedChangeBuffer(Entity entity, EntityCommandBuffer commandBuffer)
    {
        DynamicBuffer<LoadedChange> changes;

        if(!entityManager.HasComponent<LoadedChange>(entity))
            changes = commandBuffer.AddBuffer<LoadedChange>(entity);
        else
            changes = entityManager.GetBuffer<LoadedChange>(entity);

        return changes;
    }
}
