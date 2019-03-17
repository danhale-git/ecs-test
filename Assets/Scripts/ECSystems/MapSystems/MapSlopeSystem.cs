using System;
using UnityEngine;
using Unity.Jobs;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using MyComponents;

[UpdateAfter(typeof(MapBlockDataSystem))]
public class MapSlopeSystem : ComponentSystem
{
    EntityManager entityManager;

    int squareWidth;

    ComponentGroup slopeGroup;

    protected override void OnCreateManager()
    {
        entityManager = World.Active.GetOrCreateManager<EntityManager>();

        squareWidth = TerrainSettings.mapSquareWidth;
 
        //  Chunks that need blocks generating
        EntityArchetypeQuery slopeQuery = new EntityArchetypeQuery{
            None    = new ComponentType[] { typeof(Tags.EdgeBuffer), typeof(Tags.OuterBuffer) },
            All     = new ComponentType[] { typeof(MapSquare), typeof(Tags.SetSlopes), typeof(AdjacentSquares) }
        };
		slopeGroup = GetComponentGroup(slopeQuery);
    }

    protected override void OnUpdate()
    {
        EntityCommandBuffer 		commandBuffer 	= new EntityCommandBuffer(Allocator.Temp);
		NativeArray<ArchetypeChunk> chunks 			= slopeGroup.CreateArchetypeChunkArray(Allocator.TempJob);

		ArchetypeChunkEntityType                entityType = GetArchetypeChunkEntityType();
    	ArchetypeChunkComponentType<MapSquare>	squareType = GetArchetypeChunkComponentType<MapSquare>();
		ArchetypeChunkBufferType<Block> 		blocksType = GetArchetypeChunkBufferType<Block>();
		ArchetypeChunkBufferType<Topology> 		heightType = GetArchetypeChunkBufferType<Topology>();

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
					heightAccessor[e].AsNativeArray(),
					adjacentHeightMaps
				);

                //	Draw mesh next
				commandBuffer.RemoveComponent<Tags.SetSlopes>(entity);
            } 
        }
        commandBuffer.Playback(entityManager);
        commandBuffer.Dispose();

        chunks.Dispose();
    }

    //	Generate list of Y offsets for top 4 cube vertices
	void GetSlopes(MapSquare mapSquare, DynamicBuffer<Block> blocks, NativeArray<Topology> heightMap, DynamicBuffer<Topology>[] adjacentHeightMaps)
	{
		float3[] directions = Util.CardinalDirections();

		for(int h = 0; h < heightMap.Length; h++)
		{
			int height = heightMap[h].height;

			//	2D position
			float3 pos = Util.Unflatten2D(h, squareWidth);

			int blockIndex = Util.Flatten(pos.x, height - mapSquare.bottomBlockBuffer, pos.z, squareWidth);
			Block block = blocks[blockIndex];

			//	Block type is not sloped
			if(BlockTypes.sloped[block.type] == 0) continue;

			//	Height differences for all adjacent positions
			float[] differences = new float[directions.Length];
			int heightDifferenceCount = 0;

			//	Get height differences
			for(int d = 0; d < directions.Length; d++)
			{
				int x = (int)(directions[d].x + pos.x);
				int z = (int)(directions[d].z + pos.z);

				//	Direction of the adjacent map square that owns the required block
				float3 edge = Util.EdgeOverlap(new float3(x, 0, z), squareWidth);

				int adjacentHeight;

				//	Block is outside map square
				if(	edge.x != 0 || edge.z != 0)
					adjacentHeight = adjacentHeightMaps[Util.DirectionToIndex(edge)][Util.WrapAndFlatten2D(x, z, squareWidth)].height;
				//	Block is inside map square
				else
					adjacentHeight = heightMap[Util.Flatten2D(x, z, squareWidth)].height;

				//	Height difference in blocks
				int difference = adjacentHeight - height;

				if(difference != 0) heightDifferenceCount++;

				differences[d] = difference;
			}

			//	Block is not sloped
			if(heightDifferenceCount == 0) continue;
			
			//	Get vertex offsets (-1 to 1) for top vertices of block
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

			//	One vertex lowered, inner corner
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
			//	Two opposite vertices lowered, outer corner
			else if(frontRight < 0 && backLeft < 0)
			{
				slopeType = SlopeType.OUTERCORNER;	//	NWSE
				slopeFacing = SlopeFacing.NWSE;
			}
			else if(frontLeft < 0 && backRight < 0)
			{
				slopeType = SlopeType.OUTERCORNER;	//	SWNE
				slopeFacing = SlopeFacing.SWNE;
			}
			//	Not outer but two vertices lowered, flat slope
			else if(backLeft + backRight + frontLeft + frontRight == -2)
			{
				slopeType = SlopeType.FLAT;
				//	Don't need slope facing for flat slopes, only for corners
			}

			BlockSlope slope = new BlockSlope();
        
			slope.frontRightSlope 	= (sbyte)frontRight;
			slope.backRightSlope 	= (sbyte)backRight;
			slope.frontLeftSlope 	= (sbyte)frontLeft;
			slope.backLeftSlope 	= (sbyte)backLeft;
			slope.slopeType 		= slopeType;
			slope.slopeFacing 		= slopeFacing;

			block.isSloped = 1;
			block.slope = slope;
			blocks[blockIndex] = block;
		}
	}

    float GetVertexOffset(float adjacent1, float adjacent2, float diagonal)
	{
		bool anyAboveOne 			= (adjacent1 > 1 || adjacent2 > 1 || diagonal > 1);
		bool bothAdjacentAboveZero 	= (adjacent1 > 0 && adjacent2 > 0);
		bool anyAdjacentAboveZero 	= (adjacent1 > 0 || adjacent2 > 0);

		//	Vert up
		//if(bothAdjacentAboveZero && anyAboveOne) return 1;
		
		//	No change
		if(anyAdjacentAboveZero) return 0;

		//	Vert down
		return math.clamp(adjacent1 + adjacent2 + diagonal, -1, 0);
		
	}
}