using Unity;
using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using MyComponents;

public class MapUpdateSystem : ComponentSystem
{
    EntityManager entityManager;
	int cubeSize;

    ComponentGroup updateGroup;

    protected override void OnCreateManager()
    {
        entityManager = World.Active.GetOrCreateManager<EntityManager>();
		cubeSize = TerrainSettings.cubeSize;

        EntityArchetypeQuery updateQuery = new EntityArchetypeQuery
        {
            All = new ComponentType[]{ typeof(Tags.BlockChanged), typeof(BlockChange), typeof(MapSquare) }
        };

        updateGroup = GetComponentGroup(updateQuery);
    }

    protected override void OnUpdate()
    {
        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);

        ArchetypeChunkEntityType entityType = GetArchetypeChunkEntityType();
        ArchetypeChunkBufferType<BlockChange> blockChangeType = GetArchetypeChunkBufferType<BlockChange>();

        NativeArray<ArchetypeChunk> chunks = updateGroup.CreateArchetypeChunkArray(Allocator.TempJob);

        for(int c = 0; c < chunks.Length; c++)
        {
            NativeArray<Entity> entities = chunks[c].GetNativeArray(entityType);
            BufferAccessor<BlockChange> blockChanges = chunks[c].GetBufferAccessor(blockChangeType);

            for(int e = 0; e < entities.Length; e++)
            {
                Entity entity = entities[e];
                Debug.Log("changing something");

                commandBuffer.RemoveComponent<Tags.BlockChanged>(entity);
            }
        }

        commandBuffer.Playback(entityManager);
		commandBuffer.Dispose();

    	chunks.Dispose();
    }
}
