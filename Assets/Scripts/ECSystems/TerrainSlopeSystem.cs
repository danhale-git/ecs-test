using System;
using UnityEngine;
using Unity.Jobs;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using MyComponents;

[UpdateAfter(typeof(BlockSystem))]
public class TerrainSlopeSystem : ComponentSystem
{
    EntityManager entityManager;
    int cubeSize;

    EntityArchetypeQuery query;

    ArchetypeChunkEntityType                entityType;
    ArchetypeChunkComponentType<MapSquare>	squareType;
	ArchetypeChunkBufferType<Block> 		blocksType;
	ArchetypeChunkBufferType<Topology> 		heightType;

    protected override void OnCreateManager()
    {
        entityManager = World.Active.GetOrCreateManager<EntityManager>();
        cubeSize = TerrainSettings.cubeSize;
 
        //  Chunks that need blocks generating
        query = new EntityArchetypeQuery
        {
            Any     = Array.Empty<ComponentType>(),
            None    = new ComponentType[] { typeof(Tags.OuterBuffer) },
            All     = new ComponentType[] { typeof(MapSquare), typeof(Tags.SetSlopes) }
        };
    }

    protected override void OnUpdate()
    {
        entityType = GetArchetypeChunkEntityType();
        squareType = GetArchetypeChunkComponentType<MapSquare>();
		blocksType = GetArchetypeChunkBufferType<Block>();
        heightType = GetArchetypeChunkBufferType<Topology>();

        NativeArray<ArchetypeChunk> chunks;
        chunks = entityManager.CreateArchetypeChunkArray(
            query,
            Allocator.TempJob
            );

        if(chunks.Length == 0) chunks.Dispose();
        else DoSomething(chunks);
    }

    void DoSomething(NativeArray<ArchetypeChunk> chunks)
    {
        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);

        for(int c = 0; c < chunks.Length; c++)
        {
            ArchetypeChunk chunk = chunks[c];

            NativeArray<Entity>         entities        = chunk.GetNativeArray(entityType);
            NativeArray<MapSquare>	    squares			= chunk.GetNativeArray(squareType);
			BufferAccessor<Block> 	    blockAccessor 	= chunk.GetBufferAccessor(blocksType);
            BufferAccessor<Topology>    heightAccessor	= chunk.GetBufferAccessor(heightType);
            
            for(int e = 0; e < entities.Length; e++)
            {
                Entity entity = entities[e];

                //	List of adjacent square entities
				AdjacentSquares adjacentSquares = entityManager.GetComponentData<AdjacentSquares>(entity);

				//	Adjacent height maps in 8 directions
                DynamicBuffer<Topology>[] adjacentHeightMaps = new DynamicBuffer<Topology>[8];
				for(int i = 0; i < 8; i++)
					adjacentHeightMaps[i] = entityManager.GetBuffer<Topology>(adjacentSquares[i]);

                //	Vertex offsets for 4 top vertices of each block (slopes)
				GetSlopes(
					squares[e],
					blockAccessor[e],
					heightAccessor[e].ToNativeArray(),
					adjacentHeightMaps
				);

                //	Draw mesh next
				commandBuffer.RemoveComponent<Tags.SetSlopes>(entity);
                commandBuffer.AddComponent(entity, new Tags.DrawMesh());
            } 
        }
        commandBuffer.Playback(entityManager);
        commandBuffer.Dispose();

        chunks.Dispose();

    }

    //	Generate list of Y offsets for top 4 cube vertices
	void GetSlopes(MapSquare mapSquare, DynamicBuffer<Block> blocks, NativeArray<Topology> heightMap, DynamicBuffer<Topology>[] adjacentHeightMaps)
	{

		//int slopeCount = 0;
		float3[] directions = Util.CardinalDirections();
		for(int h = 0; h < heightMap.Length; h++)
		{
			int height = heightMap[h].height;

			//	2D position
			float3 pos = Util.Unflatten2D(h, cubeSize);

			//	3D position
			int blockIndex = Util.Flatten(pos.x, height - mapSquare.bottomBlockBuffer, pos.z, cubeSize);
			Block block = blocks[blockIndex];

			//	Block type is not sloped
			if(BlockTypes.sloped[block.type] == 0) continue;

			//	Height differences for all adjacent positions
			float[] differences = new float[directions.Length];

			int heightDifferenceCount = 0;

			for(int d = 0; d < directions.Length; d++)
			{
				int x = (int)(directions[d].x + pos.x);
				int z = (int)(directions[d].z + pos.z);

				//	Direction of the adjacent map square that owns the required block
				float3 edge = Util.EdgeOverlap(new float3(x, 0, z), cubeSize);

				int adjacentHeight;

				//	Block is outside map square
				if(	edge.x != 0 || edge.z != 0)
					adjacentHeight = adjacentHeightMaps[Util.DirectionToIndex(edge)][Util.WrapAndFlatten2D(x, z, cubeSize)].height;
				//	Block is inside map square
				else
					adjacentHeight = heightMap[Util.Flatten2D(x, z, cubeSize)].height;

				//	Height difference in blocks
				int difference = adjacentHeight - height;

				if(difference != 0) heightDifferenceCount++;

				differences[d] = difference;
			}

			//	Terrain is not sloped
			if(heightDifferenceCount == 0) continue;
			
			//	Get vertex offsets (-1 to 1) for top vertices of cube required for slope.
			float frontRight	= GetVertexOffset(differences[0], differences[2], differences[4]);	//	front right
			float backRight		= GetVertexOffset(differences[0], differences[3], differences[6]);	//	back right
			float frontLeft		= GetVertexOffset(differences[1], differences[2], differences[5]);	//	front left
			float backLeft 		= GetVertexOffset(differences[1], differences[3], differences[7]);	//	back left

			int changedVertexCount = 0;

			if(frontRight != 0)	changedVertexCount++;
			if(backRight != 0)	changedVertexCount++;
			if(frontLeft != 0)	changedVertexCount++;
			if(backLeft != 0)	changedVertexCount++;

			SlopeType slopeType = 0;
			SlopeFacing slopeFacing = 0;

			//	Check slope type and facing axis
			if(changedVertexCount == 1 && (frontLeft != 0 || backRight != 0))
			{
				slopeType = SlopeType.INNERCORNER;	//	NWSE
				slopeFacing = SlopeFacing.NWSE;
			}
			else if(changedVertexCount == 1 && (frontRight != 0 || backLeft != 0))
			{
				slopeType = SlopeType.INNERCORNER;	//	SWNE
				slopeFacing = SlopeFacing.SWNE;
			}
			else if(frontRight < 0 && backLeft < 0)
			{
				slopeType = SlopeType.OUTERCORNER;
				slopeFacing = SlopeFacing.NWSE;
			}
			else if(frontLeft < 0 && backRight < 0)
			{
				slopeType = SlopeType.OUTERCORNER;
				slopeFacing = SlopeFacing.SWNE;
			}
			else if(backLeft + backRight + frontLeft + frontRight == -2)
			{
				slopeType = SlopeType.FLAT;
				//	Don't need slope facing for flat slopes, only for corners
			}
			
			block.frontRightSlope = frontRight;
			block.backRightSlope = backRight;
			block.frontLeftSlope = frontLeft;
			block.backLeftSlope = backLeft;
			block.slopeType = slopeType;
			block.slopeFacing = slopeFacing;

			blocks[blockIndex] = block;
		}
	}

    float GetVertexOffset(float adjacent1, float adjacent2, float diagonal)
	{
		bool anyAboveOne = (adjacent1 > 1 || adjacent2 > 1 || diagonal > 1);
		bool bothAdjacentAboveZero = (adjacent1 > 0 && adjacent2 > 0);
		bool anyAdjacentAboveZero = (adjacent1 > 0 || adjacent2 > 0);

		//	Vert up
		//if(bothAdjacentAboveZero && anyAboveOne) return 1;
		
		//	No change
		if(anyAdjacentAboveZero) return 0;

		//	Vert down
		return math.clamp(adjacent1 + adjacent2 + diagonal, -1, 0);
		
	}
}