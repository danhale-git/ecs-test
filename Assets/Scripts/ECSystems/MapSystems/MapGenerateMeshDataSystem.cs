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

	protected override void OnDestroyManager()
	{
		runningCommandBuffer.Dispose();
	}

    protected override void OnUpdate()
    {
        if(!runningJobHandle.IsCompleted)
			return;
		
		JobCompleteAndBufferPlayback();
		ScheduleMoreJobs();        
	}

	void JobCompleteAndBufferPlayback()
	{
		runningJobHandle.Complete();

		runningCommandBuffer.Playback(entityManager);
		runningCommandBuffer.Dispose();
	}

	void ScheduleMoreJobs()
	{
        NativeArray<ArchetypeChunk> chunks = meshDataGroup.CreateArchetypeChunkArray(Allocator.TempJob);		
		
		EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.TempJob);
		JobHandle allHandles		= new JobHandle();
		JobHandle previousHandle	= new JobHandle();

        ArchetypeChunkEntityType 				entityType 		= GetArchetypeChunkEntityType();
		ArchetypeChunkComponentType<MapSquare> 	squareType		= GetArchetypeChunkComponentType<MapSquare>(true);
		ArchetypeChunkComponentType<FaceCounts> faceCountsType	= GetArchetypeChunkComponentType<FaceCounts>(true);
		ArchetypeChunkBufferType<Block> 		blocksType 		= GetArchetypeChunkBufferType<Block>(true);
		ArchetypeChunkBufferType<Faces> 		facesType 		= GetArchetypeChunkBufferType<Faces>(true);

		for(int c = 0; c < chunks.Length; c++)
		{
			ArchetypeChunk chunk = chunks[c];

			//	Get chunk data
			NativeArray<Entity> 	entities 		= chunk.GetNativeArray(entityType);
			NativeArray<MapSquare>	mapSquares			= chunk.GetNativeArray(squareType);
			NativeArray<FaceCounts>	faceCounts		= chunk.GetNativeArray(faceCountsType);
			BufferAccessor<Block> 	blockAccessor 	= chunk.GetBufferAccessor(blocksType);
			BufferAccessor<Faces> 	facesAccessor 	= chunk.GetBufferAccessor(facesType);

			for(int e = 0; e < entities.Length; e++)
			{
                MeshDataJob meshDataJob = new MeshDataJob(){
                    commandBuffer = commandBuffer,
                    entity = entities[e],
                    counts = faceCounts[e],
                    mapSquare = mapSquares[e],
                    
                    blocks = new NativeArray<Block>(blockAccessor[e].AsNativeArray(), Allocator.TempJob),
                    faces = new NativeArray<Faces>(facesAccessor[e].AsNativeArray(), Allocator.TempJob)
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
}
