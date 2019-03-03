using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Transforms;
using MyComponents;

[UpdateAfter(typeof(MapManagerSystem))]
public class MapSquareCheckSystem : ComponentSystem
{
    EntityManager entityManager;
    EntityUtil entityUtil;

    int squareWidth;

    MapManagerSystem managerSystem;

    ComponentGroup allSquaresGroup;

	protected override void OnCreateManager()
    {
        entityManager = World.Active.GetOrCreateManager<EntityManager>();
        managerSystem = World.Active.GetOrCreateManager<MapManagerSystem>();

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
        if(!managerSystem.update)
            return;

        managerSystem.mapMatrix.ResetBools();

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
            BufferAccessor<WorleyCell>  cells       = chunk.GetBufferAccessor<WorleyCell>(bufferType);

			for(int e = 0; e < entities.Length; e++)
			{
                if(ActiveCellCount(cells[e]) == 0)
                    RemoveMapSquare(entities[e], positions[e].Value, commandBuffer);
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
            if(managerSystem.cellMatrix.ItemIsSet(uniqueCells[i].indexFloat))
                activeCells++;
                
        return activeCells;
    }

    void RemoveMapSquare(Entity squareEntity, float3 squarePosition, EntityCommandBuffer commandBuffer)
    {
        UpdateNeighbouringSquares(squarePosition, commandBuffer);
        entityUtil.TryAddComponent<Tags.RemoveMapSquare>(squareEntity, commandBuffer);
        managerSystem.mapMatrix.UnsetItem(squarePosition);
    }

    void UpdateNeighbouringSquares(float3 centerSquarePosition, EntityCommandBuffer commandBuffer)
    {
        NativeArray<float3> neighbourDirections = Util.CardinalDirections(Allocator.Temp);
        for(int i = 0; i < neighbourDirections.Length; i++)
        {
            float3 neighbourPosition = centerSquarePosition + (neighbourDirections[i] * squareWidth);
            Entity squareEntity;

            bool alreadyUpdated = managerSystem.mapMatrix.GetBool(neighbourPosition);
            if(!alreadyUpdated && managerSystem.mapMatrix.TryGetItem(neighbourPosition, out squareEntity))
            {
                managerSystem.mapMatrix.SetBool(true, neighbourPosition);
                
                entityUtil.TryAddComponent<Tags.GetAdjacentSquares>(squareEntity, commandBuffer);
            }
        }
        neighbourDirections.Dispose();
    } 
}
