using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Transforms;
using MyComponents;

[UpdateAfter(typeof(MapGenerateMeshDataSystem))]
public class MapGenerateMeshDataBufferSystem : EntityCommandBufferSystem { }

public class MapGenerateMeshDataSystem : JobComponentSystem
{
    EntityManager entityManager;
    MapGenerateMeshDataBufferSystem bufferSystem;
    
    protected override void OnCreateManager()
    {
        entityManager = World.Active.GetOrCreateManager<EntityManager>();
        bufferSystem = World.Active.GetOrCreateManager<MapGenerateMeshDataBufferSystem>();

    }

    protected override JobHandle OnUpdate(JobHandle inputDependencies)
    {
        return inputDependencies;
    }

    [RequireComponentTag(typeof(Faces))]
    public struct WorleyJob : IJobProcessComponentDataWithEntity<MapSquare>
    {
        public void Execute(Entity mapSquareEntity, int jobIndex, ref MapSquare mapSquare)
        {

        }
    }
}
