using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Transforms;
using MyComponents;

[UpdateAfter(typeof(MapCellMarchingSystem))]
public class MapSquareCheckSystem : ComponentSystem
{
    EntityManager entityManager;
    EntityUtil entityUtil;

    int squareWidth;

    MapCellMarchingSystem managerSystem;

    ComponentGroup allSquaresGroup;

	protected override void OnCreateManager()
    {
        entityManager = World.Active.GetOrCreateManager<EntityManager>();
        managerSystem = World.Active.GetOrCreateManager<MapCellMarchingSystem>();

        entityUtil = new EntityUtil(entityManager);

        squareWidth = TerrainSettings.mapSquareWidth;

        EntityArchetypeQuery allSquaresQuery = new EntityArchetypeQuery{
			None = new ComponentType [] { typeof(Tags.RemoveMapSquare) },
            All = new ComponentType [] { typeof(MapSquare) }
		};
		allSquaresGroup = GetComponentGroup(allSquaresQuery);
    }

    protected override void OnUpdate()
    {
        //if(!managerSystem.update)
          //  return;

        EntityCommandBuffer         commandBuffer   = new EntityCommandBuffer(Allocator.Temp);
		NativeArray<ArchetypeChunk> chunks          = allSquaresGroup.CreateArchetypeChunkArray(Allocator.Persistent);

		ArchetypeChunkEntityType                entityType	    = GetArchetypeChunkEntityType();
        ArchetypeChunkComponentType<Position>   positionType    = GetArchetypeChunkComponentType<Position>();
        ArchetypeChunkBufferType<WorleyCell>    bufferType      = GetArchetypeChunkBufferType<WorleyCell>();

		for(int c = 0; c < chunks.Length; c++)
		{
			ArchetypeChunk chunk = chunks[c];

			NativeArray<Entity>         entities    = chunk.GetNativeArray(entityType);
            NativeArray<Position>       positions   = chunk.GetNativeArray<Position>(positionType);
            BufferAccessor<WorleyCell>  cellBuffers = chunk.GetBufferAccessor<WorleyCell>(bufferType);

			for(int e = 0; e < entities.Length; e++)
			{
                if(ActiveCellCount(cellBuffers[e]) == 0)
                    RemoveMapSquare(entities[e], positions[e].Value, commandBuffer);

                entityUtil.TryAddComponent<Tags.GetAdjacentSquares>(entities[e], commandBuffer);
            }
        }

        commandBuffer.Playback(entityManager);
		commandBuffer.Dispose();

		chunks.Dispose();
    }

    int ActiveCellCount(DynamicBuffer<WorleyCell> uniqueCells)
    {
        int activeCells = 0;
        for(int i = 0; i < uniqueCells.Length; i++)
            if(managerSystem.cellMatrix.ItemIsSet(uniqueCells[i].index))
                activeCells++;
                
        return activeCells;
    }

    void RemoveMapSquare(Entity squareEntity, float3 squarePosition, EntityCommandBuffer commandBuffer)
    {
        entityUtil.TryAddComponent<Tags.RemoveMapSquare>(squareEntity, commandBuffer);
        managerSystem.mapMatrix.UnsetItem(squarePosition);
    }
}
