using Unity.Jobs;
using Unity.Collections;
using Unity.Entities;
using MyComponents;

public struct MeshDataJob : IJob
{
    public EntityCommandBuffer commandBuffer;

    [ReadOnly] public Entity entity;
    [ReadOnly] public FaceCounts counts;
    [ReadOnly] public MapSquare mapSquare;

    [DeallocateOnJobCompletion][ReadOnly] public NativeArray<Block> blocks;
    [DeallocateOnJobCompletion][ReadOnly] public NativeArray<Faces> faces;

    public void Execute()
    {
        DynamicBuffer<MeshVertex> vertBuffer = commandBuffer.AddBuffer<MeshVertex>(entity);
        vertBuffer.ResizeUninitialized(counts.vertCount);
        DynamicBuffer<MeshNormal> normBuffer = commandBuffer.AddBuffer<MeshNormal>(entity);
        normBuffer.ResizeUninitialized(counts.vertCount);
        DynamicBuffer<MeshVertColor> colorBuffer = commandBuffer.AddBuffer<MeshVertColor>(entity);
        colorBuffer.ResizeUninitialized(counts.vertCount);

        DynamicBuffer<MeshTriangle> triBuffer = commandBuffer.AddBuffer<MeshTriangle>(entity);
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

        commandBuffer.RemoveComponent<FaceCounts>(entity);
        commandBuffer.RemoveComponent<Faces>(entity);
    }
}