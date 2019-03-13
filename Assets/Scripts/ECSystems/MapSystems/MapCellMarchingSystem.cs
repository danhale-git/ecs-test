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

    NativeQueue<int2> cellQueue;

	protected override void OnCreateManager()
    {
		entityManager = World.Active.GetOrCreateManager<EntityManager>();

        cellQueue = new NativeQueue<int2>(Allocator.Persistent);

        entityUtil = new EntityUtil(entityManager);
        worleyNoiseGen = new WorleyNoiseGenerator(
            TerrainSettings.seed,
            TerrainSettings.cellFrequency,
            TerrainSettings.cellEdgeSmoothing,
            TerrainSettings.cellularJitter
        );

        squareWidth = TerrainSettings.mapSquareWidth;

        mapSquareArchetype = entityManager.CreateArchetype(
            ComponentType.Create<Position>(),
            ComponentType.Create<RenderMeshProxy>(),
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
        GenerateWorleyNoise(initialMapSquare, currentMapSquare);
        WorleyCell startCell = entityManager.GetBuffer<WorleyCell>(initialMapSquare)[0];

        InitialiseCellMatrix(startCell.index);
        EnqueueCells(startCell.index);

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

    void EnqueueCells(int2 centerIndex)
    {
        int range = TerrainSettings.cellGenerateDistance;
        for(int x = centerIndex.x-range; x <= centerIndex.x+range; x++)
            for(int z = centerIndex.y-range; z <= centerIndex.y+range; z++)
            {
                int2 cellIndex = new int2(x, z);
                if(cellMatrix.array.ItemIsSet(cellIndex)) continue;
                
                cellQueue.Enqueue(cellIndex);
            }
    }

    protected override void OnDestroyManager()
    {
        mapMatrix.Dispose();
        cellMatrix.Dispose();
        cellQueue.Dispose();
    }

    protected override void OnUpdate()
    {
        DequeueCell();

        currentMapSquare = CurrentMapSquare();
        if(currentMapSquare.Equals(previousMapSquare)) return;
        else previousMapSquare = currentMapSquare;

        currentCellIndex = CurrentCellIndex();
        if(currentCellIndex.Equals(previousCellIndex)) return;
        else previousCellIndex = currentCellIndex;

        EnqueueCells(currentCellIndex);

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

    void DequeueCell()
    {
        CustomDebugTools.SetDebugText("cell queue", cellQueue.Count);
        if(cellQueue.Count == 0) return;

        int2 cellIndex = cellQueue.Dequeue();

        Entity cellEntity = DiscoverCell(worleyNoiseGen.CellFromIndex(cellIndex));
        cellMatrix.SetItem(cellEntity, cellIndex);
    }

    Entity DiscoverCell(WorleyCell cell)
    {
        Entity cellEntity = CreateCellEntity(cell);

        NativeList<CellMapSquare> allSquares = new NativeList<CellMapSquare>(Allocator.Temp);
        float3 startPosition = Util.VoxelOwner(cell.position, squareWidth);
        
        mapMatrix.ClearDiscoveredSquares();
        DiscoverMapSquaresRecursive(cell, startPosition, allSquares);

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
    
    void DiscoverMapSquaresRecursive(WorleyCell currentCell, float3 squarePosition, NativeList<CellMapSquare> allSquares)
    {
        NativeArray<float3> directions = Util.CardinalDirections(Allocator.Temp);

        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.TempJob);

        NativeList<Entity> entitiesToDiscover = new NativeList<Entity>(8, Allocator.Temp);
        NativeList<float3> positionsToDiscover = new NativeList<float3>(8, Allocator.Temp);

        for(int d = 0; d < 8; d++)
        {
            float3 adjacentPosition = squarePosition + (directions[d] * squareWidth);
            if(!mapMatrix.SquareIsDiscovered(adjacentPosition))
            {
                Entity mapSquareEntity;
                if(!mapMatrix.array.TryGetItem(adjacentPosition, out mapSquareEntity))
                {
                    mapSquareEntity = CreateMapSquareEntity(adjacentPosition);
                    GenerateWorleyNoise(mapSquareEntity, adjacentPosition, commandBuffer);
                }

                mapMatrix.SetAsDiscovered(true, adjacentPosition);

                entitiesToDiscover.Add(mapSquareEntity);
                positionsToDiscover.Add(adjacentPosition);
            }
        }

        directions.Dispose();

        commandBuffer.Playback(entityManager);
        commandBuffer.Dispose();

        for(int i = 0; i < entitiesToDiscover.Length; i++)
        {
            DynamicBuffer<WorleyCell> uniqueCells = entityManager.GetBuffer<WorleyCell>(entitiesToDiscover[i]);

            if(UniqueCellsContainsCell(uniqueCells, currentCell))
                {
                    allSquares.Add(new CellMapSquare{
                            entity = entitiesToDiscover[i],
                            edge = MapSquareIsAtEge(uniqueCells, currentCell)
                        }
                    );

                    DiscoverMapSquaresRecursive(currentCell, positionsToDiscover[i], allSquares);
                }
        }

        positionsToDiscover.Dispose();
        entitiesToDiscover.Dispose();
    }

    Entity CreateMapSquareEntity(float3 worldPosition)
    {
        Entity entity = entityManager.CreateEntity(mapSquareArchetype);
		entityManager.SetComponentData<Position>(entity, new Position{ Value = worldPosition } );

        mapMatrix.SetItem(entity, worldPosition);

        return entity;
    }
    
    void GenerateWorleyNoise(Entity entity, float3 worldPosition)
    {
        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.TempJob);
        GenerateWorleyNoise(entity, worldPosition, commandBuffer);
        commandBuffer.Playback(entityManager);
        commandBuffer.Dispose();
    } 
    
    void GenerateWorleyNoise(Entity entity, float3 worldPosition, EntityCommandBuffer commandBuffer)
    {
        WorleyNoiseJob cellJob = new WorleyNoiseJob(){
			offset 		    = worldPosition,
			squareWidth	    = squareWidth,
			util 		    = new JobUtil(),
            noise 		    = worleyNoiseGen,

            commandBuffer   = commandBuffer,
            mapSquareEntity = entity
        };

        cellJob.Schedule().Complete();
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
