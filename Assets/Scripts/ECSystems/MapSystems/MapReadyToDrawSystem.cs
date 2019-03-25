using UnityEngine;

using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Jobs;
using MyComponents;

[UpdateAfter(typeof(MapUpdateGroups.InitialiseSquaresGroup))]
public class MapReadyToDrawSystem : ComponentSystem
{
    EntityManager entityManager;

    ComponentGroup readySquaresGroup;

    protected override void OnCreateManager()
    {
        entityManager = World.Active.GetOrCreateManager<EntityManager>();

        EntityArchetypeQuery readySquaresQuery = new EntityArchetypeQuery
        {
            All = new ComponentType[] { typeof(Topology) },
            None = new ComponentType[] { typeof(PendingChange), typeof(Tags.LoadChanges), typeof(Tags.InitialiseStageComplete) }
        };
        readySquaresGroup = GetComponentGroup(readySquaresQuery);
    }
    
    protected override void OnUpdate()
    {
        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);

        NativeArray<ArchetypeChunk> chunks = readySquaresGroup.CreateArchetypeChunkArray(Allocator.Persistent);

        ArchetypeChunkEntityType entityType = GetArchetypeChunkEntityType();

        for(int c = 0; c < chunks.Length; c++)
        {
            ArchetypeChunk chunk = chunks[c];

            NativeArray<Entity> entities = chunk.GetNativeArray(entityType);

            for(int e = 0; e < entities.Length; e++)
            {
                commandBuffer.AddComponent<Tags.InitialiseStageComplete>(entities[e], new Tags.InitialiseStageComplete());
                //commandBuffer.AddComponent<Tags.Debug.MarkError>(entities[e], new Tags.Debug.MarkError());
            }
        }

        commandBuffer.Playback(entityManager);
        commandBuffer.Dispose();

        chunks.Dispose();
    }
}
