using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Transforms;
using MyComponents;
using UnityEngine;

[UpdateAfter(typeof(MapCellDiscoverySystem))]
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
                //if(ActiveCellCount(cellBuffers[e]) == 0)
                //    RemoveMapSquare(entities[e], positions[e].Value, commandBuffer);

                float3 position = positions[e].Value;

                /*if(managerSystem.mapMatrix.array.ItemIsSet(Util.Float3ToInt2(position)) &&
                    !entityManager.Exists(entities[e]))
                {
                    Debug.Log("item is set for non existent entity at "+position);
                }
                else
                    Debug.Log("map square is good "+entities[e]); */

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
            if(managerSystem.cellMatrix.array.ItemIsSet(uniqueCells[i].index))
                activeCells++;
                
        return activeCells;
    }

    void RemoveMapSquare(Entity squareEntity, float3 squarePosition, EntityCommandBuffer commandBuffer)
    {
        entityUtil.TryAddComponent<Tags.RemoveMapSquare>(squareEntity, commandBuffer);
        managerSystem.mapMatrix.array.UnsetItem(squarePosition);
    }
}
