using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using System.Collections.Generic;
using MyComponents;

[UpdateAfter(typeof(MapUpdateSystem))]
public class MapSaveSystem : ComponentSystem
{
    EntityManager entityManager;

	int cubeSize;

    public Dictionary<float3, SaveData[]>  allAcres            = new Dictionary<float3, SaveData[]>();
    public Dictionary<float3, bool[]>      mapSquareChanged    = new Dictionary<float3, bool[]>();

    public const int       acreSize = 16;
    ComponentGroup  saveGroup;

    public struct SaveData
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
        {
            Debug.Log("new change added "+mapSquare.position);
            changes.Add(changesBuffer[i].block);
        }
        
        //  Save map square to array
        acre[mapSquareIndex]    = new SaveData(mapSquare, changes.ToArray());
        //  Save map square array to acre
        allAcres[acrePosition]  = acre;
    }

    public float3 AcreRootPosition(float3 position)
	{
        int divisor = acreSize * cubeSize;
		int x = (int)math.floor(position.x / divisor);
		int z = (int)math.floor(position.z / divisor);
		return new float3(x*divisor, 0, z*divisor);
	}
}
