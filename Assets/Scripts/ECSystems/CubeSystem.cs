using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using MyComponents;

public class CubeSystem : ComponentSystem
{
	EntityManager entityManager;
	int cubeSize;


	ArchetypeChunkEntityType entityType;

	ArchetypeChunkComponentType<MapSquare> mapSquareType;
	ArchetypeChunkBufferType<Block> blocksType;
	ArchetypeChunkBufferType<CubePosition> cubePosType;

	EntityArchetypeQuery mapSquareQuery;

	protected override void OnCreateManager()
	{
		entityManager = World.Active.GetOrCreateManager<EntityManager>();
		cubeSize = TerrainSettings.cubeSize;

		mapSquareQuery = new EntityArchetypeQuery
		{
			Any 	= Array.Empty<ComponentType>(),
			None 	= Array.Empty<ComponentType>(),
			All  	= new ComponentType[] { typeof(MapSquare), typeof(Tags.GenerateBlocks) }
		};
	}

	protected override void OnUpdate()
	{
		entityType = GetArchetypeChunkEntityType();

		mapSquareType		= GetArchetypeChunkComponentType<MapSquare>();
		blocksType 			= GetArchetypeChunkBufferType<Block>();
		cubePosType 		= GetArchetypeChunkBufferType<CubePosition>();

		NativeArray<ArchetypeChunk> chunks;
		chunks	= entityManager.CreateArchetypeChunkArray(
						mapSquareQuery, Allocator.TempJob
						);

		if(chunks.Length == 0) chunks.Dispose();
		else GenerateCubes(chunks);
			
	}

	void GenerateCubes(NativeArray<ArchetypeChunk> chunks)
	{
		EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);

		for(int c = 0; c < chunks.Length; c++)
		{
			ArchetypeChunk chunk = chunks[c];
			NativeArray<Entity> entities 				= chunk.GetNativeArray(entityType);

			NativeArray<MapSquare> mapSquares 			= chunk.GetNativeArray(mapSquareType);
			BufferAccessor<Block> blocks 				= chunk.GetBufferAccessor(blocksType);
			BufferAccessor<CubePosition> cubePositions 	= chunk.GetBufferAccessor(cubePosType);

			for(int e = 0; e < mapSquares.Length; e++)
			{
				Entity entity = entities[e];
				float2 position = mapSquares[e].worldPosition;

				UnityEngine.Debug.Log(position);
			}
		}
	}





}
