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

    const int maxCellDistance = 150;

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

        return CreateMapSquareEntity(currentMapSquare);
    }

    void InitialiseCellMatrix(NativeArray<WorleyCell> initialCells)
    {
        cellMatrix = new GridMatrix<Entity>{
            rootPosition = initialCells[0].indexFloat,
            gridSquareSize = 1
        };
        cellMatrix.Initialise(1, Allocator.Persistent);

        for(int i = 0; i < initialCells.Length; i++)
            DiscoverCellsRecursive(initialCells[i]);
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
            DiscoverCellsRecursive(cellsInRange[i]);
        cellsInRange.Dispose();

        RemoveOutOfRangeCells();

        CustomDebugTools.SetDebugText("Cell matrix length", cellMatrix.Length);
        CustomDebugTools.SetDebugText("Map matrix length", mapMatrix.Length);
    }

    public float3 CurrentMapSquare()
    {
        float3 playerPosition = entityManager.GetComponentData<Position>(playerEntity).Value;
        return Util.VoxelOwner(playerPosition, squareWidth);
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
        return (math.abs(difference.x) < maxCellDistance && math.abs(difference.z) < maxCellDistance);
    }

    void DiscoverCellsRecursive(WorleyCell cell)
    {
        if(cellMatrix.ItemIsSet(cell.indexFloat))
            return;

        Entity cellEntity = CreateCellEntity(cell);
        cellMatrix.SetItem(cellEntity, cell.indexFloat);

        mapMatrix.ResetBools();
        NativeList<CellMapSquare> allSquares = new NativeList<CellMapSquare>(Allocator.Temp);
        float3 startPosition = Util.VoxelOwner(cell.position, squareWidth);
        DiscoverMapSquaresRecursive(cellEntity, startPosition, allSquares);

        DynamicBuffer<CellMapSquare> cellMapSquareBuffer = entityManager.GetBuffer<CellMapSquare>(cellEntity);
        cellMapSquareBuffer.CopyFrom(allSquares);

        NativeList<WorleyCell> newCells = FindUndiscoveredCells(allSquares);
        for(int i = 0; i < newCells.Length; i++)
        {
            WorleyCell newCell = newCells[i];

            if(CellIsInRange(newCell))
                DiscoverCellsRecursive(newCell);
            else
                undiscoveredCells.Add(newCell);
        }

        allSquares.Dispose();
        newCells.Dispose();
    }

    Entity CreateCellEntity(WorleyCell cell)
    {
        Entity cellEntity = entityManager.CreateEntity(worleyCellArchetype);

        DynamicBuffer<WorleyCell> cellBuffer = entityManager.GetBuffer<WorleyCell>(cellEntity);
        cellBuffer.ResizeUninitialized(1);
        cellBuffer[0] = cell;

        return cellEntity;
    }

    NativeList<WorleyCell> FindUndiscoveredCells(NativeList<CellMapSquare> allSquares)
    {
        NativeList<WorleyCell> newCells = new NativeList<WorleyCell>(Allocator.TempJob);

        for(int s = 0; s < allSquares.Length; s++)
        {
            NativeArray<WorleyCell> cells = entityUtil.CopyDynamicBuffer<WorleyCell>(allSquares[s].entity, Allocator.Temp);
            for(int c = 0; c < cells.Length; c++)
                if(!cellMatrix.ItemIsSet(cells[c].indexFloat))
                    newCells.Add(cells[c]);
            cells.Dispose();
        }
        
        NativeList<WorleyCell> newCellSet = Util.Set<WorleyCell>(newCells, Allocator.Temp);
        newCells.Dispose();

        return newCellSet;
    }

    void DiscoverMapSquaresRecursive(Entity currentCellEntity, float3 squarePosition, NativeList<CellMapSquare> allSquares)
    {
        WorleyCell currentCell = entityManager.GetBuffer<WorleyCell>(currentCellEntity)[0];

        Entity mapSquareEntity = GetOrCreateMapSquare(squarePosition);
        mapMatrix.SetBool(true, squarePosition);

        DynamicBuffer<WorleyCell> uniqueCells = entityManager.GetBuffer<WorleyCell>(mapSquareEntity);

        if(!MapSquareNeedsChecking(currentCell, uniqueCells))
            return;

        sbyte atEdge;
        if(uniqueCells.Length == 1 && uniqueCells[0].value == currentCell.value)
            atEdge = 0;
        else
            atEdge = 1;

        allSquares.Add(new CellMapSquare{
                entity = mapSquareEntity,
                edge = atEdge
            }
        );

        NativeArray<float3> directions = Util.CardinalDirections(Allocator.Temp);
        for(int d = 0; d < 4; d++)
        {
            float3 adjacentPosition = squarePosition + (directions[d] * squareWidth);
            if(!mapMatrix.GetBool(adjacentPosition))
                DiscoverMapSquaresRecursive(currentCellEntity, adjacentPosition, allSquares);
        }  
    }

    Entity GetOrCreateMapSquare(float3 position)
    {
        Entity entity;

        if(!mapMatrix.TryGetItem(position, out entity))
            return CreateMapSquareEntity(position);
        else if(!entityManager.Exists(entity))
            return CreateMapSquareEntity(position);
        else
            return entity;
    }

    Entity CreateMapSquareEntity(float3 worldPosition)
    {
        Entity entity = entityManager.CreateEntity(mapSquareArchetype);
		entityManager.SetComponentData<Position>(entity, new Position{ Value = worldPosition } );
        horizontalDrawBufferSystem.SetDrawBuffer(entity, worldPosition);

        mapMatrix.SetItem(entity, worldPosition);

        GenerateWorleyNoise(entity, worldPosition);

        return entity;
    }

    bool MapSquareNeedsChecking(WorleyCell cell, DynamicBuffer<WorleyCell> uniqueCells)
    {
        for(int i = 0; i < uniqueCells.Length; i++)
            if(uniqueCells[i].index.Equals(cell.index))
                return true;

        return false;
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

    void RemoveOutOfRangeCells()
    {
        for(int c = 0; c < cellMatrix.Length; c++)
        {
            if(!cellMatrix.ItemIsSet(c))
                continue;

            Entity cellEntity = cellMatrix.GetItem(c);
            WorleyCell cell = entityManager.GetBuffer<WorleyCell>(cellEntity)[0];

            if(!CellIsInRange(cell))
            {
                RemoveCellMapSquares(cellEntity, cell);

                cellMatrix.UnsetItem(cell.indexFloat);
                undiscoveredCells.Add(cell);
            }
        }
    }

    void RemoveCellMapSquares(Entity cellEntity, WorleyCell cell)
    {
        NativeArray<CellMapSquare> mapSquares = entityUtil.CopyDynamicBuffer<CellMapSquare>(cellEntity, Allocator.Temp);
        for(int s = 0; s < mapSquares.Length; s++)
        {
            CellMapSquare mapSquare = mapSquares[s];
            float3 squarePosition = entityManager.GetComponentData<Position>(mapSquare.entity).Value;

            if(mapSquare.edge == 0 || ActiveCellCount(mapSquare) <= 1)
                RemoveMapSquare(mapSquare.entity, squarePosition);
        }
        mapSquares.Dispose();
    }

    int ActiveCellCount(CellMapSquare mapSquare)
    {
        DynamicBuffer<WorleyCell> uniqueCells = entityManager.GetBuffer<WorleyCell>(mapSquare.entity);
        int activeCells = 0;
        for(int i = 0; i < uniqueCells.Length; i++)
            if(cellMatrix.ItemIsSet(uniqueCells[i].indexFloat))
                activeCells++;
                
        return activeCells;
    }

    void RemoveMapSquare(Entity squareEntity, float3 squarePosition)
    {
        UpdateNeighbouringSquares(squarePosition);
        entityUtil.TryAddComponent<Tags.RemoveMapSquare>(squareEntity);
        mapMatrix.UnsetItem(squarePosition);
    }

    void UpdateNeighbouringSquares(float3 centerSquarePosition)
    {
        NativeArray<float3> neighbourDirections = Util.CardinalDirections(Allocator.Temp);
        for(int i = 0; i < neighbourDirections.Length; i++)
        {
            float3 neighbourPosition = centerSquarePosition + (neighbourDirections[i] * squareWidth);
            Entity squareEntity;
            if(mapMatrix.TryGetItem(neighbourPosition, out squareEntity))
                entityUtil.TryAddComponent<Tags.GetAdjacentSquares>(squareEntity);
        }
        neighbourDirections.Dispose();
    }
}
