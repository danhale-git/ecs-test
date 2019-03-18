using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Transforms;
using MyComponents;
using UnityEngine;
using System.Collections.Generic;
[UpdateAfter(typeof(MapMeshSystem))]
public class DebugSystem : ComponentSystem
{
    EntityManager entityManager;
    MapSquareSystem squareSystem;

    EntityUtil entityUtil;

    int squareWidth;

    ComponentGroup allSquaresGroup;

    DebugLineUtil lineUtil;

    public List<Dictionary<Entity, List<DebugLineUtil.DebugLine>>> mapSquareLines = new List<Dictionary<Entity, List<DebugLineUtil.DebugLine>>>()
    {
        new Dictionary<Entity, List<DebugLineUtil.DebugLine>>(),  //  Horizontal Buffer
        new Dictionary<Entity, List<DebugLineUtil.DebugLine>>(),  //  Block buffer
        new Dictionary<Entity, List<DebugLineUtil.DebugLine>>(),  //  Mark error
        new Dictionary<Entity, List<DebugLineUtil.DebugLine>>()   //  Draw buffer
    };

	protected override void OnCreateManager()
    {
        entityManager = World.Active.GetOrCreateManager<EntityManager>();
        squareSystem = World.Active.GetOrCreateManager<MapSquareSystem>();

        entityUtil = new EntityUtil(entityManager);

        squareWidth = TerrainSettings.mapSquareWidth;

        lineUtil = new DebugLineUtil(squareWidth);

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

		ArchetypeChunkEntityType                    entityType	    = GetArchetypeChunkEntityType();
        ArchetypeChunkComponentType<MapSquare>      mapSquareType   = GetArchetypeChunkComponentType<MapSquare>();        
        ArchetypeChunkComponentType<Translation>    positionType    = GetArchetypeChunkComponentType<Translation>();
        ArchetypeChunkBufferType<WorleyCell>        bufferType      = GetArchetypeChunkBufferType<WorleyCell>();

		for(int c = 0; c < chunks.Length; c++)
		{
			ArchetypeChunk chunk = chunks[c];

			NativeArray<Entity>         entities    = chunk.GetNativeArray(entityType);
            NativeArray<MapSquare>      mapSquares  = chunk.GetNativeArray<MapSquare>(mapSquareType);
            NativeArray<Translation>    positions   = chunk.GetNativeArray<Translation>(positionType);
            BufferAccessor<WorleyCell>  cellBuffers = chunk.GetBufferAccessor<WorleyCell>(bufferType);

			for(int e = 0; e < entities.Length; e++)
			{
                Entity entity = entities[e];
                float3 position = positions[e].Value;
                MapSquare mapSquare = mapSquares[e];

                DebugHorizontalBuffer(entity, position);

                if(!entityManager.HasComponent<Tags.EdgeBuffer>(entity))
                    BlockBufferDebug(entity, position, mapSquare);
            }
        }

        commandBuffer.Playback(entityManager);
		commandBuffer.Dispose();

		chunks.Dispose();
    }

    void DebugHorizontalBuffer(Entity entity, float3 position)
    {
        mapSquareLines[0][entity] = lineUtil.CreateBox(
            new float3(position.x, 0, position.z),
            squareWidth * 0.95f,
            HorizontalBufferToColor(entity),
            0,
            0,
            noSides: true,
            topOnly: true
        );
    }

    Color HorizontalBufferToColor(Entity entity)
    {
        if(entityManager.HasComponent<Tags.InnerBuffer>(entity)) return new Color(0, 1, 0, 0.2f);
        if(entityManager.HasComponent<Tags.OuterBuffer>(entity)) return new Color(1, 0, 0, 0.2f);
        if(entityManager.HasComponent<Tags.EdgeBuffer>(entity)) return new Color(0, 0, 1, 0.1f);
        else return new Color(1, 1, 1, 0.2f);
    }

    public void BlockBufferDebug(Entity entity, float3 position, MapSquare mapSquare)
    {
        mapSquareLines[1][entity] = lineUtil.CreateBox(
            new float3(position.x, 0, position.z),
            squareWidth * 0.99f,
            new Color(0.8f, 0.8f, 0.8f, 0.1f),
            mapSquare.topBlockBuffer,
            mapSquare.bottomBlockBuffer,
            noSides: false
        );
    }

    public void DrawBufferDebug(Entity entity, float3 position, MapSquare mapSquare)
    {
        mapSquareLines[3][entity] = lineUtil.CreateBox(
            new float3(position.x, 0, position.z),
            squareWidth * 0.99f,
            new Color(0.8f, 0.8f, 0.8f, 0.1f),
            mapSquare.topDrawBounds,
            mapSquare.bottomDrawBounds,
            noSides: false
        );
    }

    public void MarkError(Entity entity, float3 position, Color color)
    {
        mapSquareLines[2][entity] = lineUtil.CreateBox(
            new float3(position.x, 0, position.z),
            squareWidth,
            color,
            TerrainSettings.terrainHeight,
            0,
            noSides: false
        );
    }
}
