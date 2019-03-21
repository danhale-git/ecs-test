using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Transforms;
using MyComponents;


[UpdateAfter(typeof(MapFaceCullingSystem))]
public class MapGenerateMeshDataSystem : ComponentSystem
{
    EntityManager entityManager;

    ComponentGroup meshDataGroup;

    JobHandle runningJobHandle;
	EntityCommandBuffer runningCommandBuffer;
    
    protected override void OnCreateManager()
    {
        entityManager = World.Active.GetOrCreateManager<EntityManager>();

        EntityArchetypeQuery meshDataQuery = new EntityArchetypeQuery{
            All = new ComponentType[] { typeof(MapSquare), typeof(FaceCounts) }
        };
        meshDataGroup = GetComponentGroup(meshDataQuery);

        runningJobHandle = new JobHandle();
		runningCommandBuffer = new EntityCommandBuffer(Allocator.TempJob);
    }

    protected override void OnUpdate()
    {
        if(runningJobHandle.IsCompleted)
		{
			UnityEngine.Debug.Log("data complete");
			runningJobHandle.Complete();

			runningCommandBuffer.Playback(entityManager);
			runningCommandBuffer.Dispose();
		}
		else
		{
			UnityEngine.Debug.Log("data running");
			return;
		}

        EntityCommandBuffer 		commandBuffer 	= new EntityCommandBuffer(Allocator.TempJob);
		JobHandle					allHandles		= new JobHandle();
		JobHandle					previousHandle		= new JobHandle();
        NativeArray<ArchetypeChunk> chunks = meshDataGroup.CreateArchetypeChunkArray(Allocator.TempJob);

        ArchetypeChunkEntityType 						entityType 		= GetArchetypeChunkEntityType();
		ArchetypeChunkComponentType<MapSquare> 			squareType		= GetArchetypeChunkComponentType<MapSquare>(true);
		ArchetypeChunkComponentType<FaceCounts> 		faceCountsType	= GetArchetypeChunkComponentType<FaceCounts>(true);
		ArchetypeChunkBufferType<Block> 				blocksType 		= GetArchetypeChunkBufferType<Block>(true);
		ArchetypeChunkBufferType<Faces> 				facesType 		= GetArchetypeChunkBufferType<Faces>(true);

		for(int c = 0; c < chunks.Length; c++)
		{
			ArchetypeChunk chunk = chunks[c];

			//	Get chunk data
			NativeArray<Entity> 			entities 		= chunk.GetNativeArray(entityType);
			NativeArray<MapSquare>			squares			= chunk.GetNativeArray(squareType);
			NativeArray<FaceCounts>		faceCounts		= chunk.GetNativeArray(faceCountsType);
			BufferAccessor<Block> 			blockAccessor 	= chunk.GetBufferAccessor(blocksType);
			BufferAccessor<Faces> 			facesAccessor 	= chunk.GetBufferAccessor(facesType);

			for(int e = 0; e < entities.Length; e++)
			{
                Entity entity = entities[e];
				MapSquare mapSquare = squares[e];
                FaceCounts counts = faceCounts[e];

                DynamicBuffer<Block> blocksBuffer = blockAccessor[e];
                DynamicBuffer<Faces> facesBuffer = facesAccessor[e];

                NativeArray<Block> blocksArray = new NativeArray<Block>(blocksBuffer.Length, Allocator.TempJob);
                blocksArray.CopyFrom(blocksBuffer.AsNativeArray());
                NativeArray<Faces> facesArray = new NativeArray<Faces>(facesBuffer.Length, Allocator.TempJob);
                facesArray.CopyFrom(facesBuffer.AsNativeArray());

                MeshDataJob meshDataJob = new MeshDataJob(){
                    commandBuffer = commandBuffer,
                    entity = entity,
                    counts = counts,
                    mapSquare = mapSquare,
                    
                    blocks = blocksArray,
                    faces = facesArray
                };

                JobHandle thisHandle = meshDataJob.Schedule(previousHandle);
				allHandles = JobHandle.CombineDependencies(thisHandle, allHandles);

				previousHandle = thisHandle;
			}
		}

		runningCommandBuffer = commandBuffer;
		runningJobHandle = allHandles;

		chunks.Dispose();
	}

    public struct MeshDataJob : IJob
    {
        public EntityCommandBuffer commandBuffer;

        [ReadOnly] public Entity entity;
        [ReadOnly] public FaceCounts counts;
        [ReadOnly] public MapSquare mapSquare;

        [DeallocateOnJobCompletion]
        [ReadOnly] public NativeArray<Block> blocks;
        [DeallocateOnJobCompletion]
        [ReadOnly] public NativeArray<Faces> faces;

        public void Execute()
        {
            DebugTools.IncrementDebugCount("block data");
				
            DynamicBuffer<VertBuffer> vertBuffer = commandBuffer.AddBuffer<VertBuffer>(entity);
            vertBuffer.ResizeUninitialized(counts.vertCount);
            DynamicBuffer<NormBuffer> normBuffer = commandBuffer.AddBuffer<NormBuffer>(entity);
            normBuffer.ResizeUninitialized(counts.vertCount);
            DynamicBuffer<ColorBuffer> colorBuffer = commandBuffer.AddBuffer<ColorBuffer>(entity);
            colorBuffer.ResizeUninitialized(counts.vertCount);

            DynamicBuffer<TriBuffer> triBuffer = commandBuffer.AddBuffer<TriBuffer>(entity);
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
}
