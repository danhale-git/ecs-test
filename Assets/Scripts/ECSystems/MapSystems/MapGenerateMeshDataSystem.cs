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
        MeshDataJob meshDataJob = new MeshDataJob{
            commandBuffer = bufferSystem.CreateCommandBuffer().ToConcurrent(),
            topologyBuffers = GetBufferFromEntity<Topology>(),

        };

        return inputDependencies;
    }

    public struct MeshDataJob : IJobProcessComponentDataWithEntity<MapSquare, FaceCounts>
    {
        public EntityCommandBuffer.Concurrent commandBuffer;

        public BufferFromEntity<Topology> topologyBuffers;
        public BufferFromEntity<Faces> facesBuffers;

        public void Execute(Entity entity, int jobIndex, ref MapSquare mapSquare, ref FaceCounts counts)
        {
            DynamicBuffer<Topology> topology = topologyBuffers[entity];
            DynamicBuffer<Faces> faces = facesBuffers[entity];

            //	Determine vertex and triangle arrays using face count
            NativeArray<float3> vertices 	= new NativeArray<float3>(counts.vertCount, Allocator.TempJob);
            NativeArray<float3> normals 	= new NativeArray<float3>(counts.vertCount, Allocator.TempJob);
            NativeArray<int> 	triangles 	= new NativeArray<int>	 (counts.triCount, Allocator.TempJob);
            NativeArray<float4> colors 		= new NativeArray<float4>(counts.vertCount, Allocator.TempJob);

            MeshGenerator meshGenerator = new MeshGenerator{
                vertices 	= vertices,
                normals 	= normals,
                triangles 	= triangles,
                colors 		= colors,

                mapSquare = mapSquare,
                faces = faces.AsNativeArray(),

                util = new JobUtil(),
                squareWidth = TerrainSettings.mapSquareWidth,

                baseVerts = new CubeVertices(true)
            };

            for(int i = 0; i < mapSquare.blockDrawArrayLength; i++)
            {
                meshGenerator.Execute(i);
            }

            DynamicBuffer<VertBuffer> vertBuffer = commandBuffer.AddBuffer<VertBuffer>(jobIndex, entity);
            vertBuffer.ResizeUninitialized(counts.vertCount);
            DynamicBuffer<NormBuffer> normBuffer = commandBuffer.AddBuffer<NormBuffer>(jobIndex, entity);
            normBuffer.ResizeUninitialized(counts.vertCount);
            DynamicBuffer<ColorBuffer> colorBuffer = commandBuffer.AddBuffer<ColorBuffer>(jobIndex, entity);
            colorBuffer.ResizeUninitialized(counts.vertCount);

            DynamicBuffer<TriBuffer> triBuffer = commandBuffer.AddBuffer<TriBuffer>(jobIndex, entity);
            triBuffer.ResizeUninitialized(counts.triCount);

            for(int i = 0; i < counts.vertCount; i++)
            {
                vertBuffer[i] = new VertBuffer{ vertex = vertices[i] };
                normBuffer[i] = new NormBuffer{ normal = normals[i] };
                colorBuffer[i] = new ColorBuffer{ color = colors[i] };

            }

            for(int i = 0; i < counts.triCount; i++)
            {
                triBuffer[i] = new TriBuffer{ triangle = triangles[i] };
            }

            vertices.Dispose();
            normals.Dispose();
            colors.Dispose();
            triangles.Dispose();

            commandBuffer.RemoveComponent<FaceCounts>(jobIndex, entity);
            commandBuffer.RemoveComponent<Faces>(jobIndex, entity);
        }
    }
}
