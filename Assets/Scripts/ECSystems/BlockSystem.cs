using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using MyComponents;

[UpdateAfter(typeof(CubeSystem))]
public class BlockSystem : ComponentSystem
{
	EntityManager entityManager;

	int cubeSize;

	ArchetypeChunkEntityType 			entityType;
	ArchetypeChunkBufferType<Block> 	blocksType;
	ArchetypeChunkBufferType<MapCube> 	cubeType;
	ArchetypeChunkBufferType<Height> 	heightmapType;

	EntityArchetypeQuery mapSquareQuery;

	protected override void OnCreateManager()
	{
		entityManager = World.Active.GetOrCreateManager<EntityManager>();
		cubeSize = TerrainSettings.cubeSize;

		mapSquareQuery = new EntityArchetypeQuery
		{
			Any 	= Array.Empty<ComponentType>(),
			None  	= new ComponentType[] { typeof(Tags.OuterBuffer) },
			All  	= new ComponentType[] { typeof(MapSquare), typeof(Tags.SetBlocks) }
		};
	}

	protected override void OnUpdate()
	{
		entityType 		= GetArchetypeChunkEntityType();

		blocksType 		= GetArchetypeChunkBufferType<Block>();
		cubeType 		= GetArchetypeChunkBufferType<MapCube>();
		heightmapType 	= GetArchetypeChunkBufferType<Height>();

		NativeArray<ArchetypeChunk> chunks = entityManager.CreateArchetypeChunkArray(
			mapSquareQuery,
			Allocator.TempJob
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
			BufferAccessor<Block> blockAccessor 		= chunk.GetBufferAccessor(blocksType);
			BufferAccessor<MapCube> cubeAccessor 		= chunk.GetBufferAccessor(cubeType);
			BufferAccessor<Height> heightmapAccessor 	= chunk.GetBufferAccessor(heightmapType);
			
			for(int e = 0; e < entities.Length; e++)
			{
				Entity entity 						= entities[e];
				DynamicBuffer<Block> blockBuffer 	= blockAccessor[e];
				DynamicBuffer<MapCube> cubes		= cubeAccessor[e];
				DynamicBuffer<Height> heightmap		= heightmapAccessor[e];

				//	Resize buffer to size of (blocks in a cube) * (number of cubes)
				int blockArrayLength = (int)math.pow(cubeSize, 3) * cubes.Length;
				blockBuffer.ResizeUninitialized(blockArrayLength);

				//	Generate block data from height map
				NativeArray<Block> blocks = GetBlocks(
					1,
					blockArrayLength,
					heightmap.ToNativeArray(),
					cubes.ToNativeArray()
					);

				//	Fill buffer
				for(int b = 0; b < blocks.Length; b++)
					blockBuffer [b] = blocks[b];

				//	Draw mesh next
				commandBuffer.RemoveComponent<Tags.SetBlocks>(entity);
                commandBuffer.AddComponent(entity, new Tags.DrawMesh());

				blocks.Dispose();
			}
		}
		
		commandBuffer.Playback(entityManager);
		commandBuffer.Dispose();

		chunks.Dispose();
	}

	NativeArray<Block> GetBlocks(int batchSize, int blockArrayLength, NativeArray<Height> heightMap, NativeArray<MapCube> cubes)
	{
		//	Block data for all cubes in the map square
		var blocks = new NativeArray<Block>(blockArrayLength, Allocator.TempJob);

		//	Size of a single cube's flattened block array
		int singleCubeArrayLength = (int)math.pow(cubeSize, 3);

		//	Iterate over one cube at a time, generating blocks for the map square
		for(int i = 0; i < cubes.Length; i++)
		{
			if(cubes[i].blocks != 1) continue;

			int cubeStart = i * singleCubeArrayLength;

			var job = new BlocksJob()
			{
				blocks = blocks,
				cubeStart = cubeStart,
				cubePosY = cubes[i].yPos,

				heightMap = heightMap,
				cubeSize = cubeSize,
				util = new JobUtil()
			};

        	job.Schedule(singleCubeArrayLength, batchSize).Complete();

			bool hasAir = false;
			bool hasSolid = false;
			
			for(int b = cubeStart; b > cubeStart + singleCubeArrayLength; b++)
			{
				if(BlockTypes.visible[blocks[i].type] > 0)
					if(!hasSolid) hasSolid = true;
				else if(!hasAir) hasAir = true;

				if(hasSolid && hasAir) break;
			}

			//	Store the composition of the cube
			MapCube cube = SetComposition(hasAir, hasSolid, cubes[i]);
			cubes[i] = cube;
		}

		return blocks;
	}

	//	TODO: support visible, see through blocks 
	//	Is all this even necessary? What is composition used for?
	MapCube SetComposition(bool hasAirBlocks, bool hasSolidBlocks, MapCube cube)
	{
		CubeComposition composition = CubeComposition.AIR;

		if(hasAirBlocks && hasSolidBlocks)		//	All air blocks
			composition = CubeComposition.AIR;	
		else if(hasAirBlocks && hasSolidBlocks)	//	Some solid blocks
			composition = CubeComposition.SOLID;
		else if(hasAirBlocks && hasSolidBlocks)	//	All solid blocks
			composition = CubeComposition.MIXED;

		return new MapCube{
			yPos = cube.yPos,
			blocks = cube.blocks,
			composition = composition
		};
	}





}
