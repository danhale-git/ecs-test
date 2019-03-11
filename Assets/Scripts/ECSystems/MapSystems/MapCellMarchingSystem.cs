using UnityEngine;
using Unity.Jobs;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Transforms;
using Unity.Rendering;
using MyComponents;

[AlwaysUpdateSystem]
public class MapCellMarchingSystem : ComponentSystem
{
    EntityManager entityManager;

    EntityUtil entityUtil;
    WorleyNoiseUtil worleyUtil;
    WorleyNoiseGenerator worleyNoiseGen;

    int squareWidth;

    public static Entity playerEntity;

    public MapMatrix<Entity> mapMatrix;
    public CellMatrix<Entity> cellMatrix;

    public float3 currentMapSquare;
    float3 previousMapSquare;
    int2 currentCellIndex;
    int2 previousCellIndex;

    EntityArchetype mapSquareArchetype;
    EntityArchetype worleyCellArchetype;

	protected override void OnCreateManager()
    {
		entityManager = World.Active.GetOrCreateManager<EntityManager>();

        entityUtil = new EntityUtil(entityManager);
        worleyUtil = new WorleyNoiseUtil();
        worleyNoiseGen = new WorleyNoiseGenerator(
            TerrainSettings.seed,
            TerrainSettings.cellFrequency,
            TerrainSettings.cellEdgeSmoothing,
            TerrainSettings.cellularJitter
        );

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
        currentMapSquare = CurrentMapSquare();
        InitialiseMapMatrix(currentMapSquare);

        Entity initialMapSquare = CreateMapSquareEntity(currentMapSquare);
        WorleyCell startCell = entityManager.GetBuffer<WorleyCell>(initialMapSquare)[0];

        InitialiseCellMatrix(startCell.index);
        GenerateCells(startCell.index);

        currentCellIndex = CurrentCellIndex();
        //  Initialise 'previous' variables to somomething that doesn't match the current position
        previousMapSquare = currentMapSquare + (100 * squareWidth);
        previousCellIndex = currentCellIndex + 100;
    }

    void InitialiseMapMatrix(float3 rootPosition)
    {
        mapMatrix = new MapMatrix<Entity>{};
        mapMatrix.Initialise(1, Allocator.Persistent, rootPosition, squareWidth);
    }

    void InitialiseCellMatrix(int2 rootPosition)
    {
        cellMatrix = new CellMatrix<Entity>{};
        cellMatrix.Initialise(1, Allocator.Persistent, rootPosition);
    }

    void GenerateCells(int2 centerIndex)
    {
        int range = TerrainSettings.cellGenerateDistance;
        for(int x = centerIndex.x-range; x <= centerIndex.x+range; x++)
            for(int z = centerIndex.y-range; z <= centerIndex.y+range; z++)
            {
                int2 cellIndex = new int2(x, z);
                if(cellMatrix.array.ItemIsSet(cellIndex)) continue;
                
                Entity cellEntity = DiscoverCell(worleyNoiseGen.CellFromIndex(cellIndex));
                cellMatrix.SetItem(cellEntity, cellIndex);
            }
    }

    protected override void OnDestroyManager()
    {
        mapMatrix.Dispose();
        cellMatrix.Dispose();
    }

    protected override void OnUpdate()
    {
        currentMapSquare = CurrentMapSquare();
        if(currentMapSquare.Equals(previousMapSquare))
            return;
        else
            previousMapSquare = currentMapSquare;

        currentCellIndex = CurrentCellIndex();
        if(currentCellIndex.Equals(previousCellIndex)) return;
        else previousCellIndex = currentCellIndex;

        GenerateCells(currentCellIndex);

        RemoveOutOfRangeCells();
    }

    public float3 CurrentMapSquare()
    {
        float3 playerPosition = entityManager.GetComponentData<Position>(playerEntity).Value;
        return Util.VoxelOwner(playerPosition, squareWidth);
    }
    public int2 CurrentCellIndex()
    {
        WorleyCell currentCell = entityManager.GetBuffer<WorleyCell>(mapMatrix.array.GetItem(currentMapSquare))[0];
        return currentCell.index;
    }

    Entity DiscoverCell(WorleyCell cell)
    {
        Entity cellEntity = CreateCellEntity(cell);

        NativeList<CellMapSquare> allSquares = new NativeList<CellMapSquare>(Allocator.Temp);
        float3 startPosition = Util.VoxelOwner(cell.position, squareWidth);
        
        mapMatrix.ClearDiscoveredSquares();
        DiscoverMapSquaresRecursive(cellEntity, startPosition, allSquares);

        DynamicBuffer<CellMapSquare> cellMapSquareBuffer = entityManager.GetBuffer<CellMapSquare>(cellEntity);
        cellMapSquareBuffer.CopyFrom(allSquares);

        allSquares.Dispose();

        return cellEntity;
    }

    Entity CreateCellEntity(WorleyCell cell)
    {
        Entity cellEntity = entityManager.CreateEntity(worleyCellArchetype);

        DynamicBuffer<WorleyCell> cellBuffer = entityManager.GetBuffer<WorleyCell>(cellEntity);
        cellBuffer.ResizeUninitialized(1);
        cellBuffer[0] = cell;

        return cellEntity;
    }


    void DiscoverMapSquaresRecursive(Entity currentCellEntity, float3 squarePosition, NativeList<CellMapSquare> allSquares)
    {
        WorleyCell currentCell = entityManager.GetBuffer<WorleyCell>(currentCellEntity)[0];

        Entity mapSquareEntity = GetOrCreateMapSquare(squarePosition);
        mapMatrix.SetAsDiscovered(true, squarePosition);

        DynamicBuffer<WorleyCell> uniqueCells = entityManager.GetBuffer<WorleyCell>(mapSquareEntity);

        if(!UniqueCellsContainsCell(uniqueCells, currentCell))
            return;

        allSquares.Add(new CellMapSquare{
                entity = mapSquareEntity,
                edge = MapSquareIsAtEge(uniqueCells, currentCell)
            }
        );

        NativeArray<float3> directions = Util.CardinalDirections(Allocator.Temp);
        for(int d = 0; d < 8; d++)
        {
            float3 adjacentPosition = squarePosition + (directions[d] * squareWidth);
            if(!mapMatrix.SquareIsDiscovered(adjacentPosition))
                DiscoverMapSquaresRecursive(currentCellEntity, adjacentPosition, allSquares);
        }
        directions.Dispose();
    }

    Entity GetOrCreateMapSquare(float3 position)
    {
        Entity entity;

        if(!mapMatrix.array.TryGetItem(position, out entity))
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

        mapMatrix.SetItem(entity, worldPosition);

        GenerateWorleyNoise(entity, worldPosition);

        return entity;
    }

    void GenerateWorleyNoise(Entity entity, float3 worldPosition)
    {
        NativeArray<WorleyNoise> worleyNoiseMap = worleyUtil.GetWorleyNoiseMap(worldPosition, worleyNoiseGen);
        DynamicBuffer<WorleyNoise> worleyNoiseBuffer = entityManager.GetBuffer<WorleyNoise>(entity);
        worleyNoiseBuffer.CopyFrom(worleyNoiseMap);

        NativeArray<WorleyCell> worleyCellSet = worleyUtil.UniqueWorleyCellSet(worleyNoiseMap);
        DynamicBuffer<WorleyCell> uniqueWorleyCells = entityManager.GetBuffer<WorleyCell>(entity);
        uniqueWorleyCells.CopyFrom(worleyCellSet);

        worleyCellSet.Dispose();
        worleyNoiseMap.Dispose();
    }

    bool UniqueCellsContainsCell(DynamicBuffer<WorleyCell> uniqueCells, WorleyCell cell)
    {
        for(int i = 0; i < uniqueCells.Length; i++)
            if(uniqueCells[i].index.Equals(cell.index))
                return true;

        return false;
    }

    sbyte MapSquareIsAtEge(DynamicBuffer<WorleyCell> uniqueCells, WorleyCell cell)
    {
        if(uniqueCells.Length == 1 && uniqueCells[0].value == cell.value)
            return 0;
        else
            return 1;
    }

    void RemoveOutOfRangeCells()
    {
        for(int c = 0; c < cellMatrix.Length; c++)
        {
            if(!cellMatrix.array.ItemIsSet(c))
                continue;

            Entity cellEntity = cellMatrix.array.GetItem(c);
            WorleyCell cell = entityManager.GetBuffer<WorleyCell>(cellEntity)[0];

            if(!cellMatrix.array.InDistanceFromGridPosition(cell.index, currentCellIndex, 2))
            {
                cellMatrix.array.UnsetItem(cell.index);
            }
        }
    }
}
