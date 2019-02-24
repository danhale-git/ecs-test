﻿using UnityEngine;
using Unity.Jobs;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Transforms;
using Unity.Rendering;
using System.Collections.Generic;
using MyComponents;

[AlwaysUpdateSystem]
public class MapManagerSystem : ComponentSystem
{
	public enum DrawBufferType { NONE, INNER, OUTER, EDGE }

    EntityManager entityManager;
    EntityUtil entityUtil;

    int squareWidth;

	public static Entity playerEntity;

    public WorldGridMatrix<Entity> mapMatrix;
    WorldGridMatrix<Entity> cellMatrix;

    NativeList<WorleyCell> undiscoveredCells;
    
    float3 currentMapSquare;
    float3 previousMapSquare;

    EntityArchetype mapSquareArchetype;
    ComponentGroup allSquaresGroup;

    EntityArchetype worleyCellArchetype;

	protected override void OnCreateManager()
    {
		entityManager = World.Active.GetOrCreateManager<EntityManager>();
        entityUtil = new EntityUtil(entityManager);
        squareWidth = TerrainSettings.mapSquareWidth;

        mapSquareArchetype = entityManager.CreateArchetype(
            ComponentType.Create<Position>(),
            ComponentType.Create<RenderMeshComponent>(),
            ComponentType.Create<MapSquare>(),
            ComponentType.Create<WorleyNoise>(),
            ComponentType.Create<WorleyCell>(),
            ComponentType.Create<Topology>(),
            ComponentType.Create<Block>(),

            ComponentType.Create<Tags.GenerateTerrain>(),
            ComponentType.Create<Tags.GetAdjacentSquares>(),
            ComponentType.Create<Tags.LoadChanges>(),
            ComponentType.Create<Tags.SetDrawBuffer>(),
            ComponentType.Create<Tags.SetBlockBuffer>(),
            ComponentType.Create<Tags.GenerateBlocks>(),
            ComponentType.Create<Tags.SetSlopes>(),
			ComponentType.Create<Tags.DrawMesh>()
		);

		EntityArchetypeQuery allSquaresQuery = new EntityArchetypeQuery{
			All = new ComponentType [] { typeof(MapSquare) }
		};
		allSquaresGroup = GetComponentGroup(allSquaresQuery);

        worleyCellArchetype = entityManager.CreateArchetype(
            ComponentType.Create<WorleyCell>(),
            ComponentType.Create<CellMapSquare>()
        );
    }

    protected override void OnStartRunning()
    {
        undiscoveredCells = new NativeList<WorleyCell>(Allocator.Persistent);

        currentMapSquare = CurrentMapSquare();
        //  Initialise previousMapSquare as different to starting square
        float3 offset = (new float3(100, 100, 100) * squareWidth);
        previousMapSquare = currentMapSquare + offset;

        Entity initialMapSquare = InitialiseMapMatrix();
        InitialiseCellMatrix(entityManager.GetBuffer<WorleyCell>(initialMapSquare));
    }

    void InitialiseCellMatrix(DynamicBuffer<WorleyCell> initialCells)
    {
        cellMatrix = new WorldGridMatrix<Entity>{
            rootPosition = initialCells[0].indexFloat,
            itemWorldSize = 1
        };
        cellMatrix.Initialise(1, Allocator.Persistent);

        DiscoverCells(currentMapSquare, initialCells.AsNativeArray());
    }

    Entity InitialiseMapMatrix()
    {
        mapMatrix = new WorldGridMatrix<Entity>{
            rootPosition = currentMapSquare,
            itemWorldSize = squareWidth
        };
        mapMatrix.Initialise(1, Allocator.Persistent);

        return NewMapSquare(currentMapSquare);
    }

    float3 CurrentMapSquare()
    {
        float3 playerPosition = entityManager.GetComponentData<Position>(playerEntity).Value;
        return Util.VoxelOwner(playerPosition, squareWidth);
    }

    protected override void OnDestroyManager()
    {
        mapMatrix.Dispose();
        cellMatrix.Dispose();
        undiscoveredCells.Dispose();
    }

    protected override void OnUpdate()
    {
        currentMapSquare = CurrentMapSquare();
        if(currentMapSquare.Equals(previousMapSquare))
            return;
        else previousMapSquare = currentMapSquare;

        UpdateDrawBuffer();      

        NativeList<WorleyCell> cellsInRange = CheckUndiscoveredCells();
        DiscoverCells(currentMapSquare, cellsInRange);
        cellsInRange.Dispose();
    }

    /*void DebugCells()
    {
        for(int i = 0; i < cellMatrix.Length; i++)
        {
            Entity entity = cellMatrix[i];
            if(!entityManager.Exists(entity))
                continue;
                
            DynamicBuffer<CellMapSquare> mapSquaresBuffer = entityManager.GetBuffer<CellMapSquare>(entity);
            NativeArray<CellMapSquare> mapSquares = new NativeArray<CellMapSquare>(mapSquaresBuffer.Length, Allocator.Temp);
            mapSquares.CopyFrom(mapSquaresBuffer.AsNativeArray());

            for(int s = 0; s < mapSquares.Length; s++)
            {
                DynamicBuffer<WorleyCell> uniqueWorleyCells = entityManager.GetBuffer<WorleyCell>(mapSquares[s].entity);
                float3 worldPosition = entityManager.GetComponentData<Position>(mapSquares[s].entity).Value;

                Color color = new Color(uniqueWorleyCells[0].value, uniqueWorleyCells[0].value, uniqueWorleyCells[0].value);

                if(mapSquares[s].edge == 1)
                    color = Color.red;

                color = new Color(1, 0, 0, 0.1f);

                CustomDebugTools.MarkError(worldPosition, color);   //DEBUG
            }

            mapSquares.Dispose();
        }
    } */

    void UpdateDrawBuffer()
	{
        EntityCommandBuffer         commandBuffer   = new EntityCommandBuffer(Allocator.Temp);
		NativeArray<ArchetypeChunk> chunks          = allSquaresGroup.CreateArchetypeChunkArray(Allocator.Persistent);

		ArchetypeChunkEntityType                entityType	    = GetArchetypeChunkEntityType();
		ArchetypeChunkComponentType<Position>   positionType    = GetArchetypeChunkComponentType<Position>(true);

		for(int c = 0; c < chunks.Length; c++)
		{
			ArchetypeChunk chunk = chunks[c];

			NativeArray<Entity> entities 	= chunk.GetNativeArray(entityType);
			NativeArray<Position> positions = chunk.GetNativeArray(positionType);

			for(int e = 0; e < entities.Length; e++)
			{
				Entity entity   = entities[e];
				float3 position = positions[e].Value;

                bool inViewRadius = mapMatrix.InDistanceFromWorldPosition(position, currentMapSquare, TerrainSettings.viewDistance);

                if(inViewRadius)
                    entityUtil.UpdateDrawBuffer(entity, GetDrawBuffer(position), commandBuffer);
                else
                    RedrawMapSquare(entity, commandBuffer);
			}
		}

        commandBuffer.Playback(entityManager);
		commandBuffer.Dispose();

		chunks.Dispose();
	}

    DrawBufferType GetDrawBuffer(float3 bufferWorldPosition)
    {
        float3 centerPosition = mapMatrix.WorldToMatrixPosition(currentMapSquare);
        float3 bufferPosition = mapMatrix.WorldToMatrixPosition(bufferWorldPosition);
        int view = TerrainSettings.viewDistance;

        if      (mapMatrix.IsOffsetFromPosition(bufferPosition, centerPosition, view)) return DrawBufferType.EDGE;
        else if (mapMatrix.IsOffsetFromPosition(bufferPosition, centerPosition, view-1)) return DrawBufferType.OUTER;
        else if (mapMatrix.IsOffsetFromPosition(bufferPosition, centerPosition, view-2)) return DrawBufferType.INNER;
        else if (!mapMatrix.InDistancceFromPosition(bufferPosition, centerPosition, view)) return DrawBufferType.EDGE;
        else return DrawBufferType.NONE;
    }

    void RedrawMapSquare(Entity entity, EntityCommandBuffer commandBuffer)
    {
        entityUtil.TryRemoveSharedComponent<RenderMesh>(entity, commandBuffer);
        entityUtil.TryAddComponent<Tags.DrawMesh>(entity, commandBuffer);
    }

    NativeList<WorleyCell> CheckUndiscoveredCells()
    {
        NativeList<WorleyCell> cellsInRange = new NativeList<WorleyCell>(Allocator.TempJob);

        for(int i = 0; i < undiscoveredCells.Length; i++)
        {
            WorleyCell cell = undiscoveredCells[i];
            if(CellInRange(cell))
            {
                cellsInRange.Add(cell);
                undiscoveredCells.RemoveAtSwapBack(i);
            }
        }

        return cellsInRange;
    } 

    bool CellInRange(WorleyCell cell)
    {
        float3 difference = cell.position - currentMapSquare;
        return (math.abs(difference.x) < 150 && math.abs(difference.z) < 150);
    }

    void DiscoverCells(float3 playerPosition, NativeArray<WorleyCell> startCells)
    {
        NativeList<WorleyCell> cellsToDiscover = new NativeList<WorleyCell>(Allocator.TempJob);
        cellsToDiscover.AddRange(startCells);

        int cellSafety = 0;
        while(cellsToDiscover.Length > 0 && cellSafety < 100)
        {
            cellSafety += WhileLoopSafetyCeck(cellSafety);

            NativeList<WorleyCell> newCellsToDiscover = new NativeList<WorleyCell>(Allocator.TempJob);

            for(int c = 0; c < cellsToDiscover.Length; c++)
            {
                Entity cellEntity = NewWorleyCell(cellsToDiscover[c]);
                
                NativeList<WorleyCell> newCells = DiscoverMapSquares(cellEntity);

                for(int i = 0; i < newCells.Length; i++)
                {
                    WorleyCell cell = newCells[i];
                    if(CellInRange(cell))
                        newCellsToDiscover.Add(cell);
                    else
                        undiscoveredCells.Add(cell);
                }
                
                newCells.Dispose();
            }

            cellsToDiscover.Dispose();
            cellsToDiscover = newCellsToDiscover;
        }

        cellsToDiscover.Dispose();
    }

    Entity NewWorleyCell(WorleyCell cell)
    {
        Entity cellEntity = entityManager.CreateEntity(worleyCellArchetype);

        DynamicBuffer<WorleyCell> cellBuffer = entityManager.GetBuffer<WorleyCell>(cellEntity);
        cellBuffer.ResizeUninitialized(1);
        cellBuffer[0] = cell;

        cellMatrix.SetItem(cellEntity, cell.indexFloat);
        cellMatrix.SetBool(true, cell.indexFloat);

        return cellEntity;
    }

    NativeList<WorleyCell> DiscoverMapSquares(Entity cellEntity)
    {
        WorleyCell cell = entityManager.GetBuffer<WorleyCell>(cellEntity)[0];

        NativeList<WorleyCell> newCellsToDiscover = new NativeList<WorleyCell>(Allocator.Temp);

        NativeList<float3> positionsToCheck = new NativeList<float3>(Allocator.TempJob);
        positionsToCheck.Add(Util.VoxelOwner(cell.position, squareWidth));

        int squareSafety = 0;
        while(positionsToCheck.Length > 0 && squareSafety < 100)
        {
            squareSafety += WhileLoopSafetyCeck(squareSafety);

            NativeList<float3> newPositionsToCheck = new NativeList<float3>(Allocator.Temp);

            for(int p = 0; p < positionsToCheck.Length; p++)
            {
                NativeList<WorleyCell> newCells = CheckAdjacentMapSquares(cellEntity, positionsToCheck[p], newPositionsToCheck);
                newCellsToDiscover.AddRange(newCells);
                newCells.Dispose();
            }

            positionsToCheck.Dispose();
            positionsToCheck = newPositionsToCheck;
        }

        positionsToCheck.Dispose();

        return newCellsToDiscover;
    }

    NativeList<WorleyCell> CheckAdjacentMapSquares(Entity currentCellEntity, float3 centerPosition, NativeList<float3> newPositionsToCheck)
    {
        WorleyCell currentCell = entityManager.GetBuffer<WorleyCell>(currentCellEntity)[0];

        NativeArray<float3> directions = Util.CardinalDirections(Allocator.Temp);
        NativeList<WorleyCell> newCells = new NativeList<WorleyCell>(Allocator.TempJob);

        NativeList<CellMapSquare> squaresInCell = new NativeList<CellMapSquare>(Allocator.Temp);

        for(int d = 0; d < 4; d++)
        {
            float3 adjacentPosition = centerPosition + (directions[d] * squareWidth);

            Entity mapSquareEntity = GetOrCreateMapSquare(adjacentPosition);
            DynamicBuffer<WorleyCell> uniqueCells = entityManager.GetBuffer<WorleyCell>(mapSquareEntity);

            if(!MapSquareNeedsChecking(currentCell, adjacentPosition, uniqueCells))
                continue;
            else
                mapMatrix.SetBool(true, adjacentPosition);
            
            sbyte atEdge;
            if(uniqueCells.Length == 1 && uniqueCells[0].value == currentCell.value)
                atEdge = 0;
            else
                atEdge = 1;

            newPositionsToCheck.Add(adjacentPosition);
            squaresInCell.Add(new CellMapSquare {
                    entity = mapSquareEntity,
                    edge = atEdge
                }
            );

            AddNewCells(uniqueCells, newCells);
        }

        DynamicBuffer<CellMapSquare> cellMapSquareBuffer = entityManager.GetBuffer<CellMapSquare>(currentCellEntity);
        cellMapSquareBuffer.AddRange(squaresInCell);
        squaresInCell.Dispose();
        
        return newCells;
    }

    Entity GetOrCreateMapSquare(float3 position)
    {
        Entity entity;

        if(!mapMatrix.TryGetItem(position, out entity))
            return NewMapSquare(position);
        else if(!entityManager.Exists(entity))
            return NewMapSquare(position);
        else
            return entity;
    }

    Entity NewMapSquare(float3 worldPosition)
    {
        Entity entity = entityManager.CreateEntity(mapSquareArchetype);
		entityManager.SetComponentData<Position>(entity, new Position{ Value = worldPosition } );
        entityUtil.SetDrawBuffer(entity, GetDrawBuffer(worldPosition));

        mapMatrix.SetItem(entity, worldPosition);

        DynamicBuffer<WorleyCell> uniqueWorleyCells = GenerateWorleyData(entity, worldPosition);
        return entity;
    }

    bool MapSquareNeedsChecking(WorleyCell cell, float3 adjacentPosition, DynamicBuffer<WorleyCell> uniqueCells)
    {
        for(int i = 0; i < uniqueCells.Length; i++)
            if(uniqueCells[i].index.Equals(cell.index) && !mapMatrix.GetBool(adjacentPosition))
                return true;

        return false;
    }

    void AddNewCells(DynamicBuffer<WorleyCell> uniqueCells, NativeList<WorleyCell> newCells)
    {
        NativeArray<WorleyCell> cells = new NativeArray<WorleyCell>(uniqueCells.Length, Allocator.Temp);
        cells.CopyFrom(uniqueCells.AsNativeArray());
        for(int i = 0; i < cells.Length; i++)
        {
            WorleyCell cell = cells[i];

            if(!cellMatrix.GetBool(cell.indexFloat))
            {
                newCells.Add(cell);
                NewWorleyCell(cell);
            }
        }
        cells.Dispose();
    }

    DynamicBuffer<WorleyCell> GenerateWorleyData(Entity entity, float3 worldPosition)
    {
        DynamicBuffer<WorleyNoise> worleyNoiseBuffer = GenerateWorleyNoise(entity, worldPosition);
        DynamicBuffer<WorleyCell> uniqueWorleyCells = GetWorleySet(entity, worleyNoiseBuffer);

        //CustomDebugTools.MarkError(worldPosition, new Color(uniqueWorleyCells[0].value, uniqueWorleyCells[0].value, uniqueWorleyCells[0].value));   //DEBUG

        return uniqueWorleyCells;
    }

    DynamicBuffer<WorleyNoise> GenerateWorleyNoise(Entity entity, float3 position)
    {
        DynamicBuffer<WorleyNoise> worleyNoiseBuffer = entityManager.GetBuffer<WorleyNoise>(entity);
        worleyNoiseBuffer.ResizeUninitialized(0);

        NativeArray<WorleyNoise> worleyNoiseMap = GetWorleyNoiseMap(position);
        worleyNoiseBuffer.AddRange(worleyNoiseMap);
        worleyNoiseMap.Dispose();

        return worleyNoiseBuffer;
    }

    NativeArray<WorleyNoise> GetWorleyNoiseMap(float3 position)
    {
        NativeArray<WorleyNoise> worleyNoiseMap = new NativeArray<WorleyNoise>((int)math.pow(squareWidth, 2), Allocator.TempJob);

        WorleyNoiseJob cellJob = new WorleyNoiseJob(){
            worleyNoiseMap 	= worleyNoiseMap,						//	Flattened 2D array of noise
			offset 		    = position,						        //	World position of this map square's local 0,0
			squareWidth	    = squareWidth,						    //	Length of one side of a square/cube
            seed 		    = TerrainSettings.seed,			        //	Perlin noise seed
            frequency 	    = TerrainSettings.cellFrequency,	    //	Perlin noise frequency
            perterbAmp      = TerrainSettings.cellEdgeSmoothing,    //  Gradient Peturb amount
            cellularJitter  = TerrainSettings.cellularJitter,       //  Randomness of cell shapes
			util 		    = new JobUtil(),				        //	Utilities
            noise 		    = new WorleyNoiseGenerator(0)	        //	FastNoise.GetSimplex adapted for Jobs
        };

        cellJob.Schedule(worleyNoiseMap.Length, 16).Complete();

        cellJob.noise.Dispose();

        return worleyNoiseMap;
    }

    DynamicBuffer<WorleyCell> GetWorleySet(Entity entity, DynamicBuffer<WorleyNoise> worleyNoiseBuffer)
    {
        NativeArray<WorleyNoise> sortedWorleyNoise = SortedWorleyNoise(worleyNoiseBuffer);

        DynamicBuffer<WorleyCell> uniqueCellsBuffer = entityManager.GetBuffer<WorleyCell>(entity);
        uniqueCellsBuffer.ResizeUninitialized(0);

        int index = 0;
        AddCellToSet(uniqueCellsBuffer, sortedWorleyNoise[0]);
        for(int i = 1; i < sortedWorleyNoise.Length; i++)
        {
            if(sortedWorleyNoise[i].currentCellValue != uniqueCellsBuffer[index].value)
            {
                index++;
                AddCellToSet(uniqueCellsBuffer, sortedWorleyNoise[i]);
            }
        }

        sortedWorleyNoise.Dispose();
        return uniqueCellsBuffer;
    }

    NativeArray<WorleyNoise> SortedWorleyNoise(DynamicBuffer<WorleyNoise> worleyNoiseBuffer)
    {
        NativeArray<WorleyNoise> sortedWorleyNoise = new NativeArray<WorleyNoise>(worleyNoiseBuffer.Length, Allocator.Temp);
        for(int i = 0; i < sortedWorleyNoise.Length; i++)
            sortedWorleyNoise[i] = worleyNoiseBuffer[i];

        sortedWorleyNoise.Sort();
        return sortedWorleyNoise;
    }

    void AddCellToSet(DynamicBuffer<WorleyCell> uniqueCells, WorleyNoise worleyNoise)
    {
        WorleyCell setItem = new WorleyCell {
            value = worleyNoise.currentCellValue,
            index = worleyNoise.currentCellIndex,
            indexFloat = new float3(worleyNoise.currentCellIndex.x, 0, worleyNoise.currentCellIndex.y),
            position = worleyNoise.currentCellPosition
        };
        uniqueCells.Add(setItem);
    }

    void RemoveMapSquares(NativeList<Entity> squaresToRemove)
    {
        for(int i = 0; i < squaresToRemove.Length; i++)
        {
            Entity entity = squaresToRemove[i];

            UpdateNeighbouringSquares(entityManager.GetComponentData<Position>(entity).Value);

            entityManager.AddComponent(entity, typeof(Tags.RemoveMapSquare));
        }
    }

    void UpdateNeighbouringSquares(float3 centerSquarePosition)
    {
        NativeArray<float3> neighbourDirections = Util.CardinalDirections(Allocator.Temp);
        for(int i = 0; i < neighbourDirections.Length; i++)
            UpdateAdjacentSquares(centerSquarePosition + (neighbourDirections[i] * squareWidth));

        neighbourDirections.Dispose();
    }

    void UpdateAdjacentSquares(float3 mapSquarePosition)
    {
        if(mapMatrix.WorldPositionIsInMatrix(mapSquarePosition))
        {
            Entity adjacent = mapMatrix.GetItem(mapSquarePosition);

            entityUtil.TryAddComponent<Tags.GetAdjacentSquares>(adjacent);
        }
    }

    int WhileLoopSafetyCeck(int safetyCount)
    {
        if(safetyCount == 99) throw new System.Exception("Maximum 99 while loop iterations exceeded");
        else return 1;
    }
}
