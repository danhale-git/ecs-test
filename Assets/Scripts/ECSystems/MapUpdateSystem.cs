using Unity;
using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Rendering;
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
            All = new ComponentType[]{ typeof(Tags.BlockChanged), typeof(PendingBlockChange), typeof(MapSquare) }
        };

        updateGroup = GetComponentGroup(updateQuery);
    }

    protected override void OnUpdate()
    {
        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);

        ArchetypeChunkEntityType entityType = GetArchetypeChunkEntityType();
        ArchetypeChunkComponentType<MapSquare> mapSquareType = GetArchetypeChunkComponentType<MapSquare>();
        ArchetypeChunkBufferType<PendingBlockChange> blockChangeType = GetArchetypeChunkBufferType<PendingBlockChange>();
        ArchetypeChunkBufferType<Block> blockType = GetArchetypeChunkBufferType<Block>();

        NativeArray<ArchetypeChunk> chunks = updateGroup.CreateArchetypeChunkArray(Allocator.TempJob);

        for(int c = 0; c < chunks.Length; c++)
        {
            NativeArray<Entity> entities = chunks[c].GetNativeArray(entityType);
            NativeArray<MapSquare> mapSquares = chunks[c].GetNativeArray(mapSquareType);
            BufferAccessor<PendingBlockChange> blockChangeBuffers = chunks[c].GetBufferAccessor(blockChangeType);
            BufferAccessor<Block> blockBuffers = chunks[c].GetBufferAccessor(blockType);

            for(int e = 0; e < entities.Length; e++)
            {
                Entity entity = entities[e];
                MapSquare mapSquare = mapSquares[e];
                DynamicBuffer<PendingBlockChange> blockChanges = blockChangeBuffers[e];
                DynamicBuffer<Block> blocks = blockBuffers[e];

                bool verticalBuffersChanged = false;


                for(int i = 0; i < blockChanges.Length; i++)
                {
                    Block newBlock = blockChanges[i].block;

                    //  Get index of existing block in same position
                    int index = Util.BlockIndex(newBlock, mapSquare, cubeSize);
                    Block oldBlock = blocks[index];

                    MapSquare updateMapSquare;

                    //  This change impacted the map square's vertical buffers
                    bool buffersChanged = CheckVerticalBuffers(newBlock, oldBlock, mapSquare, out updateMapSquare);
                    if(!verticalBuffersChanged && buffersChanged) verticalBuffersChanged = true;

                    //  Apply change in highest/lowest block for use in buffer systems
                    if(buffersChanged)
                    {
                        mapSquares[e] = updateMapSquare;
                    }

                    //  Set new block data
                    blocks[index] = newBlock;
                    
                    //  Store change for saving/loading and remove from pending
                    GetOrCreateCompletedChanges(entity, commandBuffer).Add(new CompletedBlockChange { block = newBlock });
                    blockChanges.RemoveAt(i);
                }

                NativeList<Entity> squaresToUpdate = SquaresToUpdate(entity, verticalBuffersChanged);

                for(int i = 0; i < squaresToUpdate.Length; i++)
                {
                   RedrawSquare(squaresToUpdate[i], commandBuffer);

                    if(verticalBuffersChanged)
                        RecalculateVerticalBuffers(squaresToUpdate[i], commandBuffer);
                }

                squaresToUpdate.Dispose();

                commandBuffer.RemoveComponent<Tags.BlockChanged>(entity);
            }
        }

        commandBuffer.Playback(entityManager);
		commandBuffer.Dispose();

    	chunks.Dispose();
    }

    bool CheckVerticalBuffers(Block newBlock, Block oldBlock, MapSquare mapSquare, out MapSquare updateSquare)
    {
        updateSquare = mapSquare;

        //  Changes in block translucency
        bool becomeTranslucent = (BlockTypes.translucent[oldBlock.type] == 0 && BlockTypes.translucent[newBlock.type] == 1);
        bool becomeOpaque = (BlockTypes.translucent[oldBlock.type] == 1 && BlockTypes.translucent[newBlock.type] == 0);

        //  Vertical buffer(s) need updating
        bool verticalBufferChanged = false;

        //  Block gone from opaque to translucent
        if(becomeTranslucent)
        {
            //  Bottom buffer has been exposed
            if(newBlock.localPosition.y <= mapSquare.bottomBlock)
            {
                verticalBufferChanged = true;
                updateSquare.bottomBlock = (int)newBlock.localPosition.y - 1;
            }
        }

        return verticalBufferChanged;
    }

    DynamicBuffer<CompletedBlockChange> GetOrCreateCompletedChanges(Entity entity, EntityCommandBuffer commandBuffer)
    {
        DynamicBuffer<CompletedBlockChange> changes;

        if(!entityManager.HasComponent<CompletedBlockChange>(entity))
            changes = commandBuffer.AddBuffer<CompletedBlockChange>(entity);
        else
            changes = entityManager.GetBuffer<CompletedBlockChange>(entity);
        
        return changes;
    }

    void RedrawSquare(Entity entity, EntityCommandBuffer commandBuffer)
    {
        if(entityManager.HasComponent<RenderMesh>(entity) && !entityManager.HasComponent<Tags.Redraw>(entity))
            commandBuffer.AddComponent<Tags.Redraw>(entity, new Tags.Redraw());

        if(!entityManager.HasComponent<Tags.DrawMesh>(entity))
            commandBuffer.AddComponent<Tags.DrawMesh>(entity, new Tags.DrawMesh());
    }

    void RecalculateVerticalBuffers(Entity entity, EntityCommandBuffer commandBuffer)
    {
        if(!entityManager.HasComponent<Tags.SetDrawBuffer>(entity))
            commandBuffer.AddComponent<Tags.SetDrawBuffer>(entity, new Tags.SetDrawBuffer());
        if(!entityManager.HasComponent<Tags.SetBlockBuffer>(entity))
            commandBuffer.AddComponent<Tags.SetBlockBuffer>(entity, new Tags.SetBlockBuffer());
        if(!entityManager.HasComponent<Tags.BufferChanged>(entity))
            commandBuffer.AddComponent<Tags.BufferChanged>(entity, new Tags.BufferChanged());
    }

    NativeList<Entity> SquaresToUpdate(Entity centerSquare, bool verticalBufferChanged)
    {
        NativeList<Entity> entities = new NativeList<Entity>(Allocator.TempJob);

        entities.Add(centerSquare);

        AdjacentSquares centerAdjacent = entityManager.GetComponentData<AdjacentSquares>(centerSquare);

        for(int i = 0; i < 4; i++)
        {
            Entity adjacent = centerAdjacent[i];
            entities.Add(adjacent);

            if(verticalBufferChanged)
                entities.Add(entityManager.GetComponentData<AdjacentSquares>(adjacent)[i]);
        }

        if(verticalBufferChanged)
            for(int i = 4; i < 8; i++)
                entities.Add(centerAdjacent[i]);

        return entities;
    }
}
