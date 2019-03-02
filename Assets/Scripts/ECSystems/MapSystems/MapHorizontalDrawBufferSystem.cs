using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Transforms;
using Unity.Rendering;
using MyComponents;

[UpdateAfter(typeof(MapManagerSystem))]
public class MapHorizontalDrawBufferSystem : ComponentSystem
{
	public enum DrawBufferType { NONE, INNER, OUTER, EDGE }

    EntityManager entityManager;
    EntityUtil entityUtil;

    MapManagerSystem managerSystem;

    ComponentGroup allSquaresGroup;
	protected override void OnCreateManager()
    {
        entityManager = World.Active.GetOrCreateManager<EntityManager>();
        managerSystem = World.Active.GetOrCreateManager<MapManagerSystem>();

        entityUtil = new EntityUtil(entityManager);


        EntityArchetypeQuery allSquaresQuery = new EntityArchetypeQuery{
			All = new ComponentType [] { typeof(MapSquare) }
		};
		allSquaresGroup = GetComponentGroup(allSquaresQuery);
    }

    protected override void OnUpdate()
    {
        if(managerSystem.update)
            UpdateDrawBuffer();
    }

    void UpdateDrawBuffer()
	{
        int mapSquareCount = 0;

        EntityCommandBuffer         commandBuffer   = new EntityCommandBuffer(Allocator.Temp);
		NativeArray<ArchetypeChunk> chunks          = allSquaresGroup.CreateArchetypeChunkArray(Allocator.Persistent);

		ArchetypeChunkEntityType                entityType	    = GetArchetypeChunkEntityType();
		ArchetypeChunkComponentType<Position>   positionType    = GetArchetypeChunkComponentType<Position>(true);

		for(int c = 0; c < chunks.Length; c++)
		{
			ArchetypeChunk chunk = chunks[c];

			NativeArray<Entity> entities 	= chunk.GetNativeArray(entityType);
			NativeArray<Position> positions = chunk.GetNativeArray(positionType);

			for(int e = 0; e < entities.Length; e++)
			{
				Entity entity   = entities[e];
				float3 position = positions[e].Value;

                bool inViewRadius = managerSystem.mapMatrix.InDistanceFromGridPosition(position, managerSystem.currentMapSquare, TerrainSettings.viewDistance);

                if(inViewRadius)
                    UpdateDrawBuffer(entity, position, commandBuffer);
                else
                    RedrawMapSquare(entity, commandBuffer);

                mapSquareCount++;
			}
		}

        commandBuffer.Playback(entityManager);
		commandBuffer.Dispose();

		chunks.Dispose();

        CustomDebugTools.SetDebugText("Total map squares", mapSquareCount);
	}

    public void UpdateDrawBuffer(Entity entity, float3 worldPosition, EntityCommandBuffer commandBuffer)
	{
        DrawBufferType buffer = GetDrawBuffer(worldPosition);

		switch(buffer)
		{
			//	Outer/None buffer changed to inner buffer
			case DrawBufferType.INNER:
                if(!entityUtil.TryReplaceComponent<Tags.OuterBuffer, Tags.InnerBuffer>(entity, commandBuffer))
                    entityUtil.TryAddComponent<Tags.InnerBuffer>(entity, commandBuffer);
				break;

			//	Edge/Inner buffer changed to outer buffer
			case DrawBufferType.OUTER:
                if(!entityUtil.TryReplaceComponent<Tags.EdgeBuffer, Tags.OuterBuffer>(entity, commandBuffer))
                    entityUtil.TryReplaceComponent<Tags.InnerBuffer, Tags.OuterBuffer>(entity, commandBuffer);
				break;

			//	Outer buffer changed to edge buffer
			case DrawBufferType.EDGE:
                entityUtil.TryReplaceComponent<Tags.OuterBuffer, Tags.EdgeBuffer>(entity, commandBuffer);
                break;

			//	Not a buffer
			default:
                entityUtil.TryRemoveComponent<Tags.EdgeBuffer>(entity, commandBuffer);
                entityUtil.TryRemoveComponent<Tags.InnerBuffer>(entity, commandBuffer);
				break;
		}

        CustomDebugTools.HorizontalBufferDebug(entity, (int)buffer);
	}     

    void RedrawMapSquare(Entity entity, EntityCommandBuffer commandBuffer)
    {
        entityUtil.TryRemoveSharedComponent<RenderMesh>(entity, commandBuffer);
        entityUtil.TryAddComponent<Tags.DrawMesh>(entity, commandBuffer);
    }

    public void SetDrawBuffer(Entity entity, float3 worldPosition)
    {
        DrawBufferType buffer = GetDrawBuffer(worldPosition);
        switch(buffer)
        {
            case DrawBufferType.INNER:
                entityUtil.AddComponent<Tags.InnerBuffer>(entity);
                break;
            case DrawBufferType.OUTER:
                entityUtil.AddComponent<Tags.OuterBuffer>(entity);
                break;
            case DrawBufferType.EDGE:
                entityUtil.AddComponent<Tags.EdgeBuffer>(entity);
                break;
            default:
                break;
        }

        CustomDebugTools.HorizontalBufferDebug(entity, (int)buffer);
    }

    DrawBufferType GetDrawBuffer(float3 bufferWorldPosition)
    {
        float3 centerPosition = managerSystem.mapMatrix.GridToMatrixPosition(managerSystem.currentMapSquare);
        float3 bufferPosition = managerSystem.mapMatrix.GridToMatrixPosition(bufferWorldPosition);
        int view = TerrainSettings.viewDistance;

        if      (managerSystem.mapMatrix.IsOffsetFromPosition(bufferPosition, centerPosition, view)) return DrawBufferType.EDGE;
        else if (managerSystem.mapMatrix.IsOffsetFromPosition(bufferPosition, centerPosition, view-1)) return DrawBufferType.OUTER;
        else if (managerSystem.mapMatrix.IsOffsetFromPosition(bufferPosition, centerPosition, view-2)) return DrawBufferType.INNER;
        else if (!managerSystem.mapMatrix.InDistancceFromPosition(bufferPosition, centerPosition, view)) return DrawBufferType.EDGE;
        else return DrawBufferType.NONE;
    }
}
