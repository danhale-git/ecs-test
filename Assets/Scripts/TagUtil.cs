using Unity.Entities;

public struct TagUtil
{
    EntityManager entityManager;

    public TagUtil(EntityManager entityManager)
    {
        this.entityManager = entityManager;
    }

    public void AddTag<T>(Entity entity)
    where T : struct, IComponentData
    {
        entityManager.AddComponent(entity, typeof(T));
    }
    
    public void TryAddTag<T>(Entity entity)
    where T : struct, IComponentData
    {
        if(!entityManager.HasComponent<T>(entity))
            entityManager.AddComponent(entity, typeof(T));
    }

    public void TryAddTag<T>(Entity entity, EntityCommandBuffer commandBuffer)
    where T : struct, IComponentData
    {
        if(!entityManager.HasComponent<T>(entity))
            commandBuffer.AddComponent<T>(entity, new T());
    }

    public void TryRemoveTag<T>(Entity entity, EntityCommandBuffer commandBuffer)
    where T : struct, IComponentData
    {
        if(entityManager.HasComponent<T>(entity))
            commandBuffer.RemoveComponent<T>(entity);
    }
    
    public bool TryReplaceTag<TRemove, TAdd>(Entity entity, EntityCommandBuffer commandBuffer)
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
}