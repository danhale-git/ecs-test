using UnityEngine;

using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Jobs;
using MyComponents;

[UpdateAfter(typeof(InitializationSystemGroup))]
public class MapReadyToDrawSystem : ComponentSystem
{
    EntityManager entityManager;

	public struct Ready : IComponentData { }

    ComponentGroup readySquaresGroup;

    protected override void OnCreateManager()
    {
        entityManager = World.Active.GetOrCreateManager<EntityManager>();

        EntityArchetypeQuery readySquaresQuery = new EntityArchetypeQuery
        {
            All = new ComponentType[] { typeof(Topology), typeof(AdjacentSquares) },
            None = new ComponentType[] { typeof(Ready), typeof(Tags.LoadChanges) }
        };
        readySquaresGroup = GetComponentGroup(readySquaresQuery);
    }
    
    protected override void OnUpdate()
    {
        return;
        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);

        NativeArray<ArchetypeChunk> chunks = readySquaresGroup.CreateArchetypeChunkArray(Allocator.Persistent);

        ArchetypeChunkEntityType entityType = GetArchetypeChunkEntityType();

        NativeList<Entity> entityList = new NativeList<Entity>(Allocator.TempJob);

        for(int c = 0; c < chunks.Length; c++)
        {
            ArchetypeChunk chunk = chunks[c];

            NativeArray<Entity> entities = chunk.GetNativeArray(entityType);

            for(int e = 0; e < entities.Length; e++)
            {
                commandBuffer.AddComponent<Ready>(entities[e], new Ready());
                commandBuffer.AddComponent<Tags.Debug.MarkError>(entities[e], new Tags.Debug.MarkError());
            }
        }

        commandBuffer.Playback(entityManager);
        commandBuffer.Dispose();

        chunks.Dispose();
    }
}
