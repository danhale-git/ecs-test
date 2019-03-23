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

	int squareWidth;

    ComponentGroup updateGroup;

    protected override void OnCreateManager()
    {
        entityManager = World.Active.GetOrCreateManager<EntityManager>();
        
		squareWidth = TerrainSettings.mapSquareWidth;

        EntityArchetypeQuery updateQuery = new EntityArchetypeQuery{
            All = new ComponentType[]{ typeof(PendingChange), typeof(MapSquare) }
        };
        updateGroup = GetComponentGroup(updateQuery);
    }

    protected override void OnUpdate()
    {
        EntityCommandBuffer         commandBuffer   = new EntityCommandBuffer(Allocator.Temp);
        NativeArray<ArchetypeChunk> chunks          = updateGroup.CreateArchetypeChunkArray(Allocator.TempJob);
       
        ArchetypeChunkEntityType                entityType      = GetArchetypeChunkEntityType();
        ArchetypeChunkComponentType<MapSquare>  mapSquareType   = GetArchetypeChunkComponentType<MapSquare>();
        ArchetypeChunkBufferType<Block>         blockType       = GetArchetypeChunkBufferType<Block>();
        ArchetypeChunkBufferType<PendingChange> blockChangeType = GetArchetypeChunkBufferType<PendingChange>();

        for(int c = 0; c < chunks.Length; c++)
        {
            NativeArray<Entity>             entities            = chunks[c].GetNativeArray(entityType);
            NativeArray<MapSquare>          mapSquares          = chunks[c].GetNativeArray(mapSquareType);
            BufferAccessor<Block>           blockBuffers        = chunks[c].GetBufferAccessor(blockType);
            BufferAccessor<PendingChange>   blockChangeBuffers  = chunks[c].GetBufferAccessor(blockChangeType);

            for(int e = 0; e < entities.Length; e++)
            {
                DebugTools.IncrementDebugCount("update");
                
                Entity                          entity          = entities[e];
                MapSquare                       mapSquare       = mapSquares[e];
                DynamicBuffer<Block>            blocks          = blockBuffers[e];
                DynamicBuffer<PendingChange>    pendingChanges  = blockChangeBuffers[e];

                DynamicBuffer<UnsavedChange> unsavedChanges = GetOrCreateCompletedChangeBuffer(entity, commandBuffer);

                bool verticalBufferChanged = false;
                MapSquare updatedMapSquare = mapSquare;

                for(int i = 0; i < pendingChanges.Length; i++)
                {
                    Block   newBlock    = pendingChanges[i].block;
                    int     index       = Util.BlockIndex(newBlock, mapSquare, squareWidth);
                    Block   oldBlock    = blocks[index];

                    //  Check and update map square's highest/lowest visible block
                    if(CheckVerticalBounds(newBlock, oldBlock, updatedMapSquare, out updatedMapSquare) && !verticalBufferChanged)
                        verticalBufferChanged = true;

                    //  Set new block data
                    blocks[index] = newBlock;
                    unsavedChanges.Add(new UnsavedChange { block = newBlock });
                }

                if(verticalBufferChanged) mapSquares[e] = updatedMapSquare;

                //  Clear pending changes
                commandBuffer.RemoveComponent<PendingChange>(entity);

                //  Update squares depending on whether or not buffer has changed
                UpdateSquares(entity, verticalBufferChanged, commandBuffer);
            }
        }

        commandBuffer.Playback(entityManager);
		commandBuffer.Dispose();

    	chunks.Dispose();
    }

    bool CheckVerticalBounds(Block newBlock, Block oldBlock, MapSquare mapSquare, out MapSquare updateSquare)
    {
        //  Update MapSquare component
        updateSquare = mapSquare;

        //  Changes in block translucency
        bool becomeTranslucent  = (BlockTypes.translucent[oldBlock.type] == 0 && BlockTypes.translucent[newBlock.type] == 1);
        bool becomeOpaque       = (BlockTypes.translucent[oldBlock.type] == 1 && BlockTypes.translucent[newBlock.type] == 0);

        //  Translucent block added below lowest block
        if(becomeTranslucent && newBlock.localPosition.y <= mapSquare.bottomBlock)
        {
            updateSquare.bottomBlock = (int)newBlock.localPosition.y - 1 - 5;
            return true;
        }
        //  Solid block added above highest block
        if(becomeOpaque && newBlock.localPosition.y > mapSquare.topBlock)
        {
            updateSquare.topBlock = (int)newBlock.localPosition.y + 5;
            return true;
        }

        return false;
    }

    void UpdateSquares(Entity centerSquare, bool verticalBufferChanged, EntityCommandBuffer commandBuffer)
    {
        //  Find squares that need updating
        NativeList<Entity> entities = new NativeList<Entity>(Allocator.TempJob);

        //  Graph of squares, top down
        //  B and C are only used when o (center) highest/lowest block is changed
        //  -  -  b  -  -
        //  -  c  a  c  -
        //  b  a  o  a  b
        //  -  c  a  c  -
        //  -  -  b  -  -

        //  o
        entities.Add(centerSquare);

        AdjacentSquares centerAdjacent = entityManager.GetComponentData<AdjacentSquares>(centerSquare);

        for(int i = 0; i < 4; i++)
        {
            Entity adjacent = centerAdjacent[i];
            //  a
            entities.Add(adjacent);

            //  b
            if(verticalBufferChanged)
                entities.Add(entityManager.GetComponentData<AdjacentSquares>(adjacent)[i]);
        }

        if(verticalBufferChanged)
            //  c
            for(int i = 4; i < 8; i++)
                entities.Add(centerAdjacent[i]);

        //  Update squares
        for(int i = 0; i < entities.Length; i++)
        {
            RedrawSquare(entities[i], commandBuffer);

            if(verticalBufferChanged)
                RecalculateVerticalBuffers(entities[i], commandBuffer);
        }

        entities.Dispose();
    }

    void RedrawSquare(Entity entity, EntityCommandBuffer commandBuffer)
    {
        //  Tags needed to redraw mesh. Skip if mesh is not drawn yet.
        if(!entityManager.HasComponent<RenderMesh>(entity)) return;

        if(!entityManager.HasComponent<Tags.Redraw>(entity))
            commandBuffer.AddComponent<Tags.Redraw>(entity, new Tags.Redraw());

        if(!entityManager.HasComponent<Tags.DrawMesh>(entity))
            commandBuffer.AddComponent<Tags.DrawMesh>(entity, new Tags.DrawMesh());
    }

    void RecalculateVerticalBuffers(Entity entity, EntityCommandBuffer commandBuffer)
    {
        //  Tags needed to check buffers and resize block array
        if(!entityManager.HasComponent<Tags.SetVerticalDrawBounds>(entity))
            commandBuffer.AddComponent<Tags.SetVerticalDrawBounds>(entity, new Tags.SetVerticalDrawBounds());

        if(!entityManager.HasComponent<Tags.BufferChanged>(entity))
            commandBuffer.AddComponent<Tags.BufferChanged>(entity, new Tags.BufferChanged());
    }

    public static DynamicBuffer<PendingChange> GetOrCreatePendingChangeBuffer(Entity entity, EntityManager entityManager)
    {
        DynamicBuffer<PendingChange> changes;

        if(!entityManager.HasComponent<PendingChange>(entity))
            changes = entityManager.AddBuffer<PendingChange>(entity);
        else
            changes = entityManager.GetBuffer<PendingChange>(entity);

        return changes;
    }

    DynamicBuffer<UnsavedChange> GetOrCreateCompletedChangeBuffer(Entity entity, EntityCommandBuffer commandBuffer)
    {
        DynamicBuffer<UnsavedChange> changes;

        if(!entityManager.HasComponent<UnsavedChange>(entity))
            changes = commandBuffer.AddBuffer<UnsavedChange>(entity);
        else
            changes = entityManager.GetBuffer<UnsavedChange>(entity);

        return changes;
    }
}
