using UnityEngine;
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
    EntityManager entityManager;
    MapHorizontalDrawBufferSystem horizontalDrawBufferSystem;

    EntityUtil entityUtil;

    int squareWidth;

	public static Entity playerEntity;

    public GridMatrix<Entity> mapMatrix;
    GridMatrix<Entity> cellMatrix;

    NativeList<WorleyCell> undiscoveredCells;
    
    public bool update;
    public float3 currentMapSquare;
    float3 previousMapSquare;

    EntityArchetype mapSquareArchetype;

    EntityArchetype worleyCellArchetype;

	protected override void OnCreateManager()
    {
		entityManager = World.Active.GetOrCreateManager<EntityManager>();
        horizontalDrawBufferSystem = World.Active.GetOrCreateManager<MapHorizontalDrawBufferSystem>();

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
        InitialiseCellMatrix(entityManager.GetBuffer<WorleyCell>(initialMapSquare).AsNativeArray());
    }

    protected override void OnDestroyManager()
    {
        mapMatrix.Dispose();
        cellMatrix.Dispose();
        undiscoveredCells.Dispose();
    }

    Entity InitialiseMapMatrix()
    {
        mapMatrix = new GridMatrix<Entity>{
            rootPosition = currentMapSquare,
            gridSquareSize = squareWidth
        };
        mapMatrix.Initialise(1, Allocator.Persistent);

        return NewMapSquare(currentMapSquare);
    }

    void InitialiseCellMatrix(NativeArray<WorleyCell> initialCells)
    {
        cellMatrix = new GridMatrix<Entity>{
            rootPosition = initialCells[0].indexFloat,
            gridSquareSize = 1
        };
        cellMatrix.Initialise(1, Allocator.Persistent);

        for(int i = 0; i < initialCells.Length; i++)
            DiscoverCells(initialCells[i]);
    }

    public float3 CurrentMapSquare()
    {
        float3 playerPosition = entityManager.GetComponentData<Position>(playerEntity).Value;
        return Util.VoxelOwner(playerPosition, squareWidth);
    }

    protected override void OnUpdate()
    {
        currentMapSquare = CurrentMapSquare();
        if(currentMapSquare.Equals(previousMapSquare))
        {
            update = false;
            return;
        }
        else
        {
            update = true;
            previousMapSquare = currentMapSquare;   
        }

        NativeList<WorleyCell> cellsInRange = UndiscoveredCellsInRange();

        for(int i = 0; i < cellsInRange.Length; i++)
            DiscoverCells(cellsInRange[i]);

        cellsInRange.Dispose();

        RemoveOutOfRangeCells();

        CustomDebugTools.SetDebugText("Cell matrix length", cellMatrix.Length);
        CustomDebugTools.currentMatrix = cellMatrix;
    }

    NativeList<WorleyCell> UndiscoveredCellsInRange()
    {
        NativeList<WorleyCell> cellsInRange = new NativeList<WorleyCell>(Allocator.TempJob);

        for(int i = 0; i < undiscoveredCells.Length; i++)
        {
            WorleyCell cell = undiscoveredCells[i];
            if(CellIsInRange(cell))
            {
                cellsInRange.Add(cell);
                undiscoveredCells.RemoveAtSwapBack(i);
            }
        }

        return cellsInRange;
    }

    bool CellIsInRange(WorleyCell cell)
    {
        float3 difference = cell.position - currentMapSquare;
        return (math.abs(difference.x) < 150 && math.abs(difference.z) < 150);
    }

    void RemoveOutOfRangeCells()
    {
        for(int c = 0; c < cellMatrix.Length; c++)
        {
            Entity cellEntity = cellMatrix.GetItem(c);

            //TODO why is this necessary?
            if(!entityManager.Exists(cellEntity))
                continue;

            WorleyCell cell = entityManager.GetBuffer<WorleyCell>(cellEntity)[0];

            if(!CellIsInRange(cell))
            {
                DynamicBuffer<CellMapSquare> mapSquaresBuffer = entityManager.GetBuffer<CellMapSquare>(cellEntity);

                NativeArray<CellMapSquare> mapSquares = new NativeArray<CellMapSquare>(mapSquaresBuffer.Length, Allocator.Temp);
                mapSquares.CopyFrom(mapSquaresBuffer.AsNativeArray());

                for(int s = 0; s < mapSquares.Length; s++)
                {
                    Entity squareEntity = mapSquares[s].entity;
        
                    //TODO why is this necessary?
                    if(!entityManager.Exists(squareEntity))
                        continue;

                    float3 squarePosition = entityManager.GetComponentData<Position>(squareEntity).Value;
                    DynamicBuffer<WorleyCell> uniqueCells = entityManager.GetBuffer<WorleyCell>(squareEntity);
                    if(MapSquareEligibleForRemoval(squareEntity, cell, uniqueCells))
                    {
                        UpdateNeighbourAdjacentSquares(squarePosition);
                        entityUtil.TryAddComponent<Tags.RemoveMapSquare>(squareEntity);
                        mapMatrix.SetBool(false, squarePosition);
                    }
                }

                cellMatrix.SetBool(false, cell.indexFloat);
                undiscoveredCells.Add(cell);

                mapSquares.Dispose();
            }
        }
    }

    void UpdateNeighbourAdjacentSquares(float3 centerSquarePosition)
    {
        NativeArray<float3> neighbourDirections = Util.CardinalDirections(Allocator.Temp);
        for(int i = 0; i < neighbourDirections.Length; i++)
            UpdateAdjacentSquares(centerSquarePosition + (neighbourDirections[i] * squareWidth));

        neighbourDirections.Dispose();
    }

    void UpdateAdjacentSquares(float3 mapSquarePosition)
    {
        Entity squareEntity = mapMatrix.GetItem(mapSquarePosition);
        if(mapMatrix.GridPositionIsInMatrix(mapSquarePosition) && entityManager.Exists(squareEntity))
        {
            Entity adjacent = mapMatrix.GetItem(mapSquarePosition);
            entityUtil.TryAddComponent<Tags.GetAdjacentSquares>(adjacent);
        }
    }

    bool MapSquareEligibleForRemoval(Entity squareEntity, WorleyCell cell, DynamicBuffer<WorleyCell> uniqueCells)
    {
        for(int i = 0; i < uniqueCells.Length; i++)
            if(CellIsInRange(uniqueCells[i]) && cellMatrix.GetBool(cell.indexFloat))
                return false;

        return true;
    }

    void DiscoverCells(WorleyCell cell)
    {
        if(cellMatrix.GetBool(cell.indexFloat))
            return;

        Entity cellEntity = NewWorleyCell(cell);

        cellMatrix.SetItem(cellEntity, cell.indexFloat);
        cellMatrix.SetBool(true, cell.indexFloat);

        NativeList<CellMapSquare> allSquares = new NativeList<CellMapSquare>(Allocator.Temp);
        float3 startPosition = Util.VoxelOwner(cell.position, squareWidth);
        DiscoverMapSquares(cellEntity, startPosition, allSquares);

        DynamicBuffer<CellMapSquare> cellMapSquareBuffer = entityManager.GetBuffer<CellMapSquare>(cellEntity);
        cellMapSquareBuffer.CopyFrom(allSquares);

        NativeList<WorleyCell> newCells = FindUndiscoveredCells(allSquares);

        for(int i = 0; i < newCells.Length; i++)
        {
            WorleyCell newCell = newCells[i];

            if(CellIsInRange(newCell))
                DiscoverCells(newCell);
            else
                undiscoveredCells.Add(newCell);
        }

        allSquares.Dispose();
        newCells.Dispose();
    }

    Entity NewWorleyCell(WorleyCell cell)
    {
        Entity cellEntity = entityManager.CreateEntity(worleyCellArchetype);

        DynamicBuffer<WorleyCell> cellBuffer = entityManager.GetBuffer<WorleyCell>(cellEntity);
        cellBuffer.ResizeUninitialized(1);
        cellBuffer[0] = cell;

        return cellEntity;
    }

    NativeList<WorleyCell> FindUndiscoveredCells(NativeList<CellMapSquare> allSquares)
    {
        NativeList<WorleyCell> newCells = new NativeList<WorleyCell>(Allocator.Temp);

        for(int s = 0; s < allSquares.Length; s++)
        {
            DynamicBuffer<WorleyCell> uniqueCells = entityManager.GetBuffer<WorleyCell>(allSquares[s].entity);

            NativeArray<WorleyCell> cells = new NativeArray<WorleyCell>(uniqueCells.Length, Allocator.Temp);
            cells.CopyFrom(uniqueCells.AsNativeArray());
            for(int c = 0; c < cells.Length; c++)
            {
                WorleyCell cell = cells[c];

                if(!cellMatrix.GetBool(cell.indexFloat))
                {
                    newCells.Add(cell);
                }
            }
            cells.Dispose();
        }
        
        return newCells;
    }

    void DiscoverMapSquares(Entity currentCellEntity, float3 position, NativeList<CellMapSquare> allSquares)
    {
        WorleyCell currentCell = entityManager.GetBuffer<WorleyCell>(currentCellEntity)[0];

        Entity mapSquareEntity = GetOrCreateMapSquare(position);
        DynamicBuffer<WorleyCell> uniqueCells = entityManager.GetBuffer<WorleyCell>(mapSquareEntity);

        if(!MapSquareNeedsChecking(currentCell, position, uniqueCells))
            return;

        mapMatrix.SetBool(true, position);

        sbyte atEdge;
        if(uniqueCells.Length == 1 && uniqueCells[0].value == currentCell.value)
            atEdge = 0;
        else
            atEdge = 1;

        allSquares.Add(new CellMapSquare {
                entity = mapSquareEntity,
                edge = atEdge
            }
        );

        NativeArray<float3> directions = Util.CardinalDirections(Allocator.Temp);
        for(int d = 0; d < 4; d++)
        {
            float3 adjacentPosition = position + (directions[d] * squareWidth);
            DiscoverMapSquares(currentCellEntity, adjacentPosition, allSquares);
        }  
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

    bool MapSquareNeedsChecking(WorleyCell cell, float3 mapSquarePosition, DynamicBuffer<WorleyCell> uniqueCells)
    {
        for(int i = 0; i < uniqueCells.Length; i++)
            if(uniqueCells[i].index.Equals(cell.index) && !mapMatrix.GetBool(mapSquarePosition))
                return true;

        return false;
    }

    Entity NewMapSquare(float3 worldPosition)
    {
        Entity entity = entityManager.CreateEntity(mapSquareArchetype);
		entityManager.SetComponentData<Position>(entity, new Position{ Value = worldPosition } );
        horizontalDrawBufferSystem.SetDrawBuffer(entity, worldPosition);

        mapMatrix.SetItem(entity, worldPosition);

        GenerateWorleyNoise(entity, worldPosition);

        return entity;
    }

    void GenerateWorleyNoise(Entity entity, float3 worldPosition)
    {
        NativeArray<WorleyNoise> worleyNoiseMap = GetWorleyNoiseMap(worldPosition);
        DynamicBuffer<WorleyNoise> worleyNoiseBuffer = entityManager.GetBuffer<WorleyNoise>(entity);
        worleyNoiseBuffer.CopyFrom(worleyNoiseMap);

        NativeArray<WorleyCell> worleyCellSet = UniqueWorleyCellSet(worldPosition, worleyNoiseMap);
        DynamicBuffer<WorleyCell> uniqueWorleyCells = entityManager.GetBuffer<WorleyCell>(entity);
        uniqueWorleyCells.CopyFrom(worleyCellSet);

        worleyCellSet.Dispose();
        worleyNoiseMap.Dispose();
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
    
    NativeArray<WorleyCell> UniqueWorleyCellSet(float3 worldPosition, NativeArray<WorleyNoise> worleyNoiseMap)
    {
        NativeList<WorleyNoise> noiseSet = Util.Set<WorleyNoise>(worleyNoiseMap, Allocator.Temp);
        NativeArray<WorleyCell> cellSet = new NativeArray<WorleyCell>(noiseSet.Length, Allocator.TempJob);

        for(int i = 0; i < noiseSet.Length; i++)
        {
            WorleyNoise worleyNoise = noiseSet[i];

            WorleyCell cell = new WorleyCell {
                value = worleyNoise.currentCellValue,
                index = worleyNoise.currentCellIndex,
                indexFloat = new float3(worleyNoise.currentCellIndex.x, 0, worleyNoise.currentCellIndex.y),
                position = worleyNoise.currentCellPosition
            };

            cellSet[i] = cell;
        }

        return cellSet;
    }
}
