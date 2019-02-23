using Unity.Entities;
using MyComponents;

public struct EntityUtil
{
    EntityManager entityManager;

    public EntityUtil(EntityManager entityManager)
    {
        this.entityManager = entityManager;
    }

    public void AddComponent<T>(Entity entity)
    where T : struct, IComponentData
    {
        entityManager.AddComponent(entity, typeof(T));
    }
    
    public void TryAddComponent<T>(Entity entity)
    where T : struct, IComponentData
    {
        if(!entityManager.HasComponent<T>(entity))
            entityManager.AddComponent(entity, typeof(T));
    }

    public void TryAddComponent<T>(Entity entity, EntityCommandBuffer commandBuffer)
    where T : struct, IComponentData
    {
        if(!entityManager.HasComponent<T>(entity))
            commandBuffer.AddComponent<T>(entity, new T());
    }

    public void TryRemoveComponent<T>(Entity entity, EntityCommandBuffer commandBuffer)
    where T : struct, IComponentData
    {
        if(entityManager.HasComponent<T>(entity))
            commandBuffer.RemoveComponent<T>(entity);
    }

    public void TryRemoveSharedComponent<T>(Entity entity, EntityCommandBuffer commandBuffer)
    where T : struct, ISharedComponentData
    {
        if(entityManager.HasComponent<T>(entity))
            commandBuffer.RemoveComponent<T>(entity);
    }
    
    public bool TryReplaceComponent<TRemove, TAdd>(Entity entity, EntityCommandBuffer commandBuffer)
    where TRemove : struct, IComponentData
    where TAdd : struct, IComponentData
    {
        if(entityManager.HasComponent<TRemove>(entity))
        {
            commandBuffer.RemoveComponent<TRemove>(entity);
            commandBuffer.AddComponent<TAdd>(entity, new TAdd());
            return true;
        }
        else return false;
    }

    public void UpdateDrawBuffer(Entity entity, MapManagerSystem.DrawBufferType buffer, EntityCommandBuffer commandBuffer)
	{
		switch(buffer)
		{
			//	Outer/None buffer changed to inner buffer
			case MapManagerSystem.DrawBufferType.INNER:
                if(!TryReplaceComponent<Tags.OuterBuffer, Tags.InnerBuffer>(entity, commandBuffer))
                    TryAddComponent<Tags.InnerBuffer>(entity, commandBuffer);
				break;

			//	Edge/Inner buffer changed to outer buffer
			case MapManagerSystem.DrawBufferType.OUTER:
                if(!TryReplaceComponent<Tags.EdgeBuffer, Tags.OuterBuffer>(entity, commandBuffer))
                    TryReplaceComponent<Tags.InnerBuffer, Tags.OuterBuffer>(entity, commandBuffer);
				break;

			//	Outer buffer changed to edge buffer
			case MapManagerSystem.DrawBufferType.EDGE:
                TryReplaceComponent<Tags.OuterBuffer, Tags.EdgeBuffer>(entity, commandBuffer);
                break;

			//	Not a buffer
			default:
                TryRemoveComponent<Tags.EdgeBuffer>(entity, commandBuffer);
                TryRemoveComponent<Tags.InnerBuffer>(entity, commandBuffer);
				break;
		}

        CustomDebugTools.HorizontalBufferDebug(entity, (int)buffer);
	}

    public void SetDrawBuffer(Entity entity, MapManagerSystem.DrawBufferType buffer)
    {
        switch(buffer)
        {
            case MapManagerSystem.DrawBufferType.INNER:
                AddComponent<Tags.InnerBuffer>(entity);
                break;
            case MapManagerSystem.DrawBufferType.OUTER:
                AddComponent<Tags.OuterBuffer>(entity);
                break;
            case MapManagerSystem.DrawBufferType.EDGE:
                AddComponent<Tags.EdgeBuffer>(entity);
                break;
            default:
                break;
        }

        CustomDebugTools.HorizontalBufferDebug(entity, (int)buffer);
    }
}