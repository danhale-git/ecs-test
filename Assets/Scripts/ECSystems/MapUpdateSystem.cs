using Unity;
using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Rendering;
using MyComponents;

[UpdateAfter(typeof(MapManagerSystem))]
public class MapUpdateSystem : ComponentSystem
{
    EntityManager entityManager;
	int cubeSize;

    ComponentGroup updateGroup;

    protected override void OnCreateManager()
    {
        entityManager = World.Active.GetOrCreateManager<EntityManager>();
		cubeSize = TerrainSettings.cubeSize;

        EntityArchetypeQuery updateQuery = new EntityArchetypeQuery{
            All = new ComponentType[]{ typeof(Tags.BlockChanged), typeof(PendingChange), typeof(MapSquare) }
        };

        updateGroup = GetComponentGroup(updateQuery);
    }

    protected override void OnUpdate()
    {
        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);

        ArchetypeChunkEntityType entityType = GetArchetypeChunkEntityType();

        ArchetypeChunkComponentType<MapSquare>  mapSquareType   = GetArchetypeChunkComponentType<MapSquare>();
        ArchetypeChunkBufferType<Block>         blockType       = GetArchetypeChunkBufferType<Block>();
        ArchetypeChunkBufferType<PendingChange> blockChangeType = GetArchetypeChunkBufferType<PendingChange>();

        NativeArray<ArchetypeChunk> chunks = updateGroup.CreateArchetypeChunkArray(Allocator.TempJob);

        for(int c = 0; c < chunks.Length; c++)
        {
            NativeArray<Entity> entities = chunks[c].GetNativeArray(entityType);

            NativeArray<MapSquare>          mapSquares          = chunks[c].GetNativeArray(mapSquareType);
            BufferAccessor<Block>           blockBuffers        = chunks[c].GetBufferAccessor(blockType);
            BufferAccessor<PendingChange>   blockChangeBuffers  = chunks[c].GetBufferAccessor(blockChangeType);

            for(int e = 0; e < entities.Length; e++)
            {
                Entity entity = entities[e];

                MapSquare                       mapSquare       = mapSquares[e];
                DynamicBuffer<Block>            blocks          = blockBuffers[e];
                DynamicBuffer<PendingChange>    pendingChanges  = blockChangeBuffers[e];

                bool verticalBufferChanged = false;

                for(int i = 0; i < pendingChanges.Length; i++)
                {
                    Block newBlock = pendingChanges[i].block;

                    //  Get index of block in array
                    int index = Util.BlockIndex(newBlock, mapSquare, cubeSize);

                    Block oldBlock = blocks[index];

                    //  Check and update map square's highest/lowest visible block
                    MapSquare updateMapSquare;
                    bool buffersChanged = CheckVerticalBounds(newBlock, oldBlock, mapSquare, out updateMapSquare);
                    if(buffersChanged)
                    {
                        mapSquares[e] = updateMapSquare;
                        if(!verticalBufferChanged) verticalBufferChanged = true;
                    }

                    //  Set new block data
                    blocks[index] = newBlock;
                }

                //  Save and clear pending changes
                MapManagerSystem.SaveMapSquare(mapSquare, pendingChanges);
                pendingChanges.Clear();

                //  Square to update depends on whether or not buffer has changed
                UpdateSquares(entity, verticalBufferChanged, commandBuffer);

                commandBuffer.RemoveComponent<Tags.BlockChanged>(entity);
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

        //  Vertical buffer(s) need updating
        bool verticalBufferChanged = false;

        //  Block changed from opaque to translucent
        if(becomeTranslucent)
        {
            //  Bottom buffer has been exposed
            if(newBlock.localPosition.y <= mapSquare.bottomBlock)
            {
                verticalBufferChanged = true;
                updateSquare.bottomBlock = (int)newBlock.localPosition.y - 5;
            }
        }
        //  Block changed from translucent to opaque
        if(becomeOpaque)
        {
            if(newBlock.localPosition.y > mapSquare.topBlock)
            {
                verticalBufferChanged = true;
                updateSquare.topBlock = (int)newBlock.localPosition.y + 4;
            }
        }

        return verticalBufferChanged;
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
        if(!entityManager.HasComponent<Tags.SetDrawBuffer>(entity))
            commandBuffer.AddComponent<Tags.SetDrawBuffer>(entity, new Tags.SetDrawBuffer());

        if(!entityManager.HasComponent<Tags.SetBlockBuffer>(entity))
            commandBuffer.AddComponent<Tags.SetBlockBuffer>(entity, new Tags.SetBlockBuffer());

        if(!entityManager.HasComponent<Tags.BufferChanged>(entity))
            commandBuffer.AddComponent<Tags.BufferChanged>(entity, new Tags.BufferChanged());
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
}
