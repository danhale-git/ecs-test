using Unity.Entities;
using Unity.Collections;
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

    public NativeArray<T> CopyDynamicBuffer<T>(Entity entity, Allocator label)
    where T : struct, IBufferElementData
    {
        DynamicBuffer<T> buffer = entityManager.GetBuffer<T>(entity);
        NativeArray<T> array = new NativeArray<T>(buffer.Length, label);
        array.CopyFrom(buffer.AsNativeArray());
        return array;
    }
}