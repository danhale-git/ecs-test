using Unity.Entities;
using Unity.Collections;
using MyComponents;

[UpdateAfter(typeof(MapSaveLoadSystem))]
public class MapRemoveSystem : ComponentSystem
{
    EntityManager entityManager;

    static int cubeSize;

    ComponentGroup removeGroup;

    protected override void OnCreateManager()
    {
        entityManager = World.Active.GetOrCreateManager<EntityManager>();

        cubeSize = TerrainSettings.cubeSize;

        EntityArchetypeQuery removeQuery = new EntityArchetypeQuery{
            All = new ComponentType[] { typeof(MapSquare), typeof(Tags.RemoveMapSquare) }
        };
        removeGroup = GetComponentGroup(removeQuery);
    }

    protected override void OnUpdate()
    {
        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);

        ArchetypeChunkEntityType entityType = GetArchetypeChunkEntityType();

        NativeArray<ArchetypeChunk> chunks = removeGroup.CreateArchetypeChunkArray(Allocator.TempJob);

        for(int c = 0; c < chunks.Length; c++)
        {
            NativeArray<Entity> entities = chunks[c].GetNativeArray(entityType);

            for(int e = 0; e < entities.Length; e++)
            {
                Entity entity = entities[e];

                commandBuffer.DestroyEntity(entity);
            }
        }

        commandBuffer.Playback(entityManager);
		commandBuffer.Dispose();

    	chunks.Dispose();
    }
}
