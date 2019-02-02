using Unity.Entities;
using Unity.Collections;
using MyComponents;

[UpdateAfter(typeof(MapSaveSystem))]
public class MapRemoveSystem : ComponentSystem
{
    EntityManager entityManager;

    static int squareWidth;

    ComponentGroup removeGroup;

    protected override void OnCreateManager()
    {
        entityManager = World.Active.GetOrCreateManager<EntityManager>();

        squareWidth = TerrainSettings.mapSquareWidth;

        EntityArchetypeQuery removeQuery = new EntityArchetypeQuery{
            All = new ComponentType[] { typeof(MapSquare), typeof(Tags.RemoveMapSquare) }
        };
        removeGroup = GetComponentGroup(removeQuery);
    }

    protected override void OnUpdate()
    {
        EntityCommandBuffer         commandBuffer   = new EntityCommandBuffer(Allocator.Temp);
        NativeArray<ArchetypeChunk> chunks          = removeGroup.CreateArchetypeChunkArray(Allocator.TempJob);

        ArchetypeChunkEntityType entityType = GetArchetypeChunkEntityType();


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
