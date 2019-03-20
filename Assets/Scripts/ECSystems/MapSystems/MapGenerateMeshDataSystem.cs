using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Transforms;
using MyComponents;

[UpdateAfter(typeof(MapGenerateMeshDataSystem))]
public class MapGenerateMeshDataBufferSystem : EntityCommandBufferSystem { }

[UpdateAfter(typeof(MapMeshSystem))]
public class MapGenerateMeshDataSystem : JobComponentSystem
{
    MapGenerateMeshDataBufferSystem commandBufferSystem;
    
    protected override void OnCreateManager()
    {
        commandBufferSystem = World.Active.GetOrCreateManager<MapGenerateMeshDataBufferSystem>();
    }

    protected override JobHandle OnUpdate(JobHandle inputDependencies)
    {
        MeshDataJob meshDataJob = new MeshDataJob{
            commandBuffer = commandBufferSystem.CreateCommandBuffer().ToConcurrent(),
            blocksBuffers    = GetBufferFromEntity<Block>(),
            facesBuffers    = GetBufferFromEntity<Faces>()
        };

        JobHandle handle = meshDataJob.Schedule(this, inputDependencies);
        commandBufferSystem.AddJobHandleForProducer(handle);

        return handle;
    }

    public struct MeshDataJob : IJobProcessComponentDataWithEntity<MapSquare, FaceCounts>
    {
        public EntityCommandBuffer.Concurrent commandBuffer;

        [ReadOnly] public BufferFromEntity<Block> blocksBuffers;
        [ReadOnly] public BufferFromEntity<Faces> facesBuffers;

        public void Execute(Entity entity, int jobIndex, ref MapSquare mapSquare, ref FaceCounts counts)
        {
            DynamicBuffer<Block> blocks = blocksBuffers[entity];
            DynamicBuffer<Faces> faces = facesBuffers[entity];

            DynamicBuffer<VertBuffer> vertBuffer = commandBuffer.AddBuffer<VertBuffer>(jobIndex, entity);
            vertBuffer.ResizeUninitialized(counts.vertCount);
            DynamicBuffer<NormBuffer> normBuffer = commandBuffer.AddBuffer<NormBuffer>(jobIndex, entity);
            normBuffer.ResizeUninitialized(counts.vertCount);
            DynamicBuffer<ColorBuffer> colorBuffer = commandBuffer.AddBuffer<ColorBuffer>(jobIndex, entity);
            colorBuffer.ResizeUninitialized(counts.vertCount);

            DynamicBuffer<TriBuffer> triBuffer = commandBuffer.AddBuffer<TriBuffer>(jobIndex, entity);
            triBuffer.ResizeUninitialized(counts.triCount);

            MeshGenerator meshGenerator = new MeshGenerator{
                vertices 	= vertBuffer,
                normals 	= normBuffer,
                triangles 	= triBuffer,
                colors 		= colorBuffer,

                mapSquare = mapSquare,

                blocks = blocks,
                faces = faces,

                util = new JobUtil(),
                squareWidth = TerrainSettings.mapSquareWidth,

                baseVerts = new CubeVertices(true)
            };

            for(int i = 0; i < mapSquare.blockDrawArrayLength; i++)
            {
                meshGenerator.Execute(i);
            }

            //commandBuffer.AddComponent(jobIndex, entity, new Tags.Debug.MarkError());

            commandBuffer.RemoveComponent<FaceCounts>(jobIndex, entity);
            commandBuffer.RemoveComponent<Faces>(jobIndex, entity);
        }
    }
}
