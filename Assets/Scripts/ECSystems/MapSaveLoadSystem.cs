﻿using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using System.Collections.Generic;
using MyComponents;

[UpdateAfter(typeof(MapUpdateSystem))]
public class MapSaveLoadSystem : ComponentSystem
{
    EntityManager entityManager;

	int cubeSize;

    Dictionary<float3, SaveData[]>  allAcres            = new Dictionary<float3, SaveData[]>();
    Dictionary<float3, bool[]>      mapSquareChanged    = new Dictionary<float3, bool[]>();

    const int       acreSize = 16;
    ComponentGroup  saveGroup;

    struct SaveData
    {
        public MapSquare mapSquare;
        public Block[] changes;
        public SaveData(MapSquare mapSquare, Block[] changes)
        {
            this.mapSquare  = mapSquare;
            this.changes    = changes;
        }
    }

	protected override void OnCreateManager()
    {
        entityManager = World.Active.GetOrCreateManager<EntityManager>();

		cubeSize = TerrainSettings.cubeSize;

        EntityArchetypeQuery saveQuery = new EntityArchetypeQuery{
            All = new ComponentType[] { typeof(MapSquare), typeof(Tags.RemoveMapSquare), typeof(UnsavedChange) }
        };
        saveGroup = GetComponentGroup(saveQuery);
    }

    protected override void OnUpdate()
    {
        Save();
    }

    public void Save()
    {
        EntityCommandBuffer         commandBuffer   = new EntityCommandBuffer(Allocator.Temp);
        NativeArray<ArchetypeChunk> chunks          = saveGroup.CreateArchetypeChunkArray(Allocator.TempJob);
        ArchetypeChunkEntityType    entityType      = GetArchetypeChunkEntityType();

        ArchetypeChunkComponentType<MapSquare>      mapSquareType   = GetArchetypeChunkComponentType<MapSquare>();
        ArchetypeChunkBufferType<UnsavedChange>   changeType      = GetArchetypeChunkBufferType<UnsavedChange>();
    
        for(int c = 0; c < chunks.Length; c++)
        {
            NativeArray<Entity>             entities        = chunks[c].GetNativeArray(entityType);
            NativeArray<MapSquare>          mapSquares      = chunks[c].GetNativeArray(mapSquareType);
            BufferAccessor<UnsavedChange>   changeBuffers   = chunks[c].GetBufferAccessor(changeType);

            for(int e = 0; e < entities.Length; e++)
            {
                //Entity entity = entities[e];

                SaveMapSquare(mapSquares[e], changeBuffers[e]);
            }
        }

        commandBuffer.Playback(entityManager);
        commandBuffer.Dispose();

        chunks.Dispose();
    }

    public void SaveMapSquare(MapSquare mapSquare, DynamicBuffer<UnsavedChange> changesBuffer)
    {
        int     acreArrayLength         = (int)math.pow(acreSize, 2);
        float3  acrePosition            = AcreRootPosition(mapSquare.position);
        float3  mapSquareMatrixIndex    = (mapSquare.position - acrePosition) / cubeSize;
        int     mapSquareIndex          = Util.Flatten2D(mapSquareMatrixIndex.x, mapSquareMatrixIndex.z, acreSize);

        List<Block>         changes = new List<Block>();
        SaveData[]    acre;

        if(!allAcres.TryGetValue(acrePosition, out acre))
        {
            CustomDebugTools.IncrementDebugCount("Acres saved");
            
            //  New acre, create dictionary entries
            acre                  = new SaveData[acreArrayLength];
            mapSquareChanged[acrePosition]  = new bool[acreArrayLength];
        }
        else
        {
            //  Existing acre, check map square for changes and add
            if(mapSquareChanged[acrePosition][mapSquareIndex])
                changes.AddRange(acre[mapSquareIndex].changes);
        }

        //  This map square has been changed
        mapSquareChanged[acrePosition][mapSquareIndex] = true;

        //  Add new changes
        for(int i = 0; i < changesBuffer.Length; i++)
            changes.Add(changesBuffer[i].block);
        
        //  Save map square to array
        acre[mapSquareIndex]    = new SaveData(mapSquare, changes.ToArray());
        //  Save map square array to acre
        allAcres[acrePosition]  = acre;
    }

    void ApplyChanges(Entity entity, SaveData data, EntityCommandBuffer commandBuffer)
    {
        DynamicBuffer<LoadedChange> appliedChanges = GetOrCreateLoadedChangeBuffer(entity, commandBuffer);

        //  Apply saved block changes
        for(int i = 0; i < data.changes.Length; i++)
            appliedChanges.Add(new LoadedChange { block = data.changes[i] });

        //  Apply saved map square
        commandBuffer.SetComponent<MapSquare>(entity, data.mapSquare);
    }

    bool LoadMapSquareChanges(float3 squarePosition, out SaveData data)
    {
        data = new SaveData();
         //  Index of map square in acre matrix
        float3  acrePosition            = AcreRootPosition(squarePosition);
        float3  mapSquareMatrixIndex    = (squarePosition - acrePosition) / cubeSize;
        int     mapSquareIndex          = Util.Flatten2D(mapSquareMatrixIndex.x, mapSquareMatrixIndex.z, acreSize);

        SaveData[] acre;

        //  Acre does not exist
        if(!allAcres.TryGetValue(acrePosition, out acre))
            return false;

        //  Map square has no changes
        if(mapSquareChanged[acrePosition][mapSquareIndex])
            return false;

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

    public float3 AcreRootPosition(float3 position)
	{
        int divisor = acreSize * cubeSize;
		int x = (int)math.floor(position.x / divisor);
		int z = (int)math.floor(position.z / divisor);
		return new float3(x*divisor, 0, z*divisor);
	}
}
