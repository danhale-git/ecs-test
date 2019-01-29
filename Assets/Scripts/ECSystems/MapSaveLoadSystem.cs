using UnityEngine;
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

    Dictionary<float3, SavedMapSquare[]> allAcres = new Dictionary<float3, SavedMapSquare[]>();
    Dictionary<float3, bool[]> mapSquareChanged = new Dictionary<float3, bool[]>();
    const int acreSize = 16;
    ComponentGroup saveGroup;

    struct SavedMapSquare
    {
        public MapSquare mapSquare;
        public Block[] changes;
        public SavedMapSquare(MapSquare mapSquare, Block[] changes)
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
            All = new ComponentType[] { typeof(MapSquare), typeof(Tags.RemoveMapSquare), typeof(CompletedChange) }
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
        ArchetypeChunkEntityType    entityType      = GetArchetypeChunkEntityType();
        NativeArray<ArchetypeChunk> chunks          = saveGroup.CreateArchetypeChunkArray(Allocator.TempJob);

        ArchetypeChunkComponentType<MapSquare>      mapSquareType   = GetArchetypeChunkComponentType<MapSquare>();
        ArchetypeChunkBufferType<CompletedChange>   changeType      = GetArchetypeChunkBufferType<CompletedChange>();
    
        for(int c = 0; c < chunks.Length; c++)
        {
            NativeArray<Entity>             entities        = chunks[c].GetNativeArray(entityType);
            NativeArray<MapSquare>          mapSquares      = chunks[c].GetNativeArray(mapSquareType);
            BufferAccessor<CompletedChange> changeBuffers   = chunks[c].GetBufferAccessor(changeType);

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

    public void SaveMapSquare(MapSquare mapSquare, DynamicBuffer<CompletedChange> changesBuffer)
    {
        int     acreArrayLength         = (int)math.pow(acreSize, 2);
        float3  acrePosition            = AcreRootPosition(mapSquare.position);
        float3  mapSquareMatrixIndex    = (mapSquare.position - acrePosition) / cubeSize;
        int     mapSquareIndex          = Util.Flatten2D(mapSquareMatrixIndex.x, mapSquareMatrixIndex.z, acreSize);

        List<Block>         changes = new List<Block>();
        SavedMapSquare[]    acre;

        if(!allAcres.TryGetValue(acrePosition, out acre))
        {
            CustomDebugTools.IncrementDebugCount("Acres saved");
            
            //  New acre, create dictionary entries
            acre                  = new SavedMapSquare[acreArrayLength];
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
        
        //  Assign changes to acre and acre to all acres
        acre[mapSquareIndex] = new SavedMapSquare(mapSquare, changes.ToArray());
        allAcres[acrePosition] = acre;
    }

    void ApplyChanges(Entity entity, Block[] changes, EntityCommandBuffer commandBuffer)
    {
        DynamicBuffer<LoadedChange> appliedChanges = GetOrCreateLoadedChangeBuffer(entity, commandBuffer);

        for(int i = 0; i < changes.Length; i++)
        {
            appliedChanges.Add(new LoadedChange { block = changes[i] });

            int y = (int)changes[i].worldPosition.y;

            
        }
    }

    /*bool LoadMapSquareChanges(float3 squarePosition, out Block[] changes)
    {
        changes = new Block[0];
         //  Index of map square in acre matrix
        float3 acrePosition = AcreRootPosition(squarePosition);
        float3 squareIndex = (squarePosition - acrePosition) / cubeSize;

        Block[][] acre;
        if(!allAcres.TryGetValue(acrePosition, out acre))
            return false;

        //  Index of map square in flattened acre array
        int flatIndex = Util.Flatten2D(squareIndex.x, squareIndex.z, acreSize);

        //  Map square has no changes
        if(acre[flatIndex] == null)
            return false;

        changes = acre[flatIndex];
        return true;
    } */

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
