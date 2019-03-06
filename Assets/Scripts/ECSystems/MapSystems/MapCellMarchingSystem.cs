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
    MapHorizontalDrawBufferSystem horizontalDrawBufferSystem;

    EntityUtil entityUtil;
    WorleyNoiseUtil worleyUtil;

    int squareWidth;

    public static Entity playerEntity;

    public GridMatrix<Entity> mapMatrix;

    public float3 currentMapSquare;
    float3 previousMapSquare;

    EntityArchetype mapSquareArchetype;
    EntityArchetype worleyCellArchetype;

	protected override void OnCreateManager()
    {
		entityManager = World.Active.GetOrCreateManager<EntityManager>();
        horizontalDrawBufferSystem = World.Active.GetOrCreateManager<MapHorizontalDrawBufferSystem>();

        entityUtil = new EntityUtil(entityManager);
        worleyUtil = new WorleyNoiseUtil();

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

        WorleyNoiseGenerator noiseGen = new WorleyNoiseGenerator(0);
        WorleyCell testCell = noiseGen.CellFromIndex(new int2(0,0),
                                                        TerrainSettings.seed,
                                                        TerrainSettings.cellFrequency,
                                                        TerrainSettings.cellularJitter);


        //////////////////////////////
    
        Entity initialMapSquare = InitialiseMapMatrix();
        DynamicBuffer<WorleyCell> initialCellsBuffer = entityManager.GetBuffer<WorleyCell>(initialMapSquare);
        NativeArray<WorleyCell> initialCells = new NativeArray<WorleyCell>(initialCellsBuffer.Length, Allocator.Temp);
        initialCells.CopyFrom(initialCellsBuffer.AsNativeArray());

        //for(int i = 0; i < initialCells.Length; i++)
        //DiscoverCell(initialCells[0]);

        for(int x = -1; x <= 1; x++)
            for(int z = -1; z <= 1; z++)
            {
                int2 cellIndex = new int2(x, z);
                WorleyCell cell = noiseGen.CellFromIndex(cellIndex,
                                                        TerrainSettings.seed,
                                                        TerrainSettings.cellFrequency,
                                                        TerrainSettings.cellularJitter);

                DiscoverCell(cell);
            }



        string debugMatrix = CustomDebugTools.PrintMatrix(mapMatrix.GetMatrix());
        Debug.Log(debugMatrix);

        noiseGen.Dispose();
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

    protected override void OnDestroyManager()
    {
        mapMatrix.Dispose();
    }

    protected override void OnUpdate()
    {
        currentMapSquare = CurrentMapSquare();
        if(currentMapSquare.Equals(previousMapSquare))
        {
            //update = false;
            return;
        }
        else
        {
            //update = true;
            previousMapSquare = currentMapSquare;   
        }
    }

    public float3 CurrentMapSquare()
    {
        float3 playerPosition = entityManager.GetComponentData<Position>(playerEntity).Value;
        return Util.VoxelOwner(playerPosition, squareWidth);
    }

    void DiscoverCell(WorleyCell cell)
    {
        //if(cellMatrix.ItemIsSet(cell.indexFloat))
            //return;

        Entity cellEntity = CreateCellEntity(cell);

        NativeList<CellMapSquare> allSquares = new NativeList<CellMapSquare>(Allocator.Temp);
        float3 startPosition = Util.VoxelOwner(cell.position, squareWidth);
        DiscoverMapSquaresRecursive(cellEntity, startPosition, allSquares);

        DynamicBuffer<CellMapSquare> cellMapSquareBuffer = entityManager.GetBuffer<CellMapSquare>(cellEntity);
        cellMapSquareBuffer.CopyFrom(allSquares);

        allSquares.Dispose();
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
        mapMatrix.SetBool(true, squarePosition);

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
            if(!mapMatrix.GetBool(adjacentPosition))
                DiscoverMapSquaresRecursive(currentCellEntity, adjacentPosition, allSquares);
        }
        directions.Dispose();
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
        //horizontalDrawBufferSystem.SetDrawBuffer(entity, worldPosition);

        mapMatrix.SetItem(entity, worldPosition);

        GenerateWorleyNoise(entity, worldPosition);

        return entity;
    }

    void GenerateWorleyNoise(Entity entity, float3 worldPosition)
    {
        NativeArray<WorleyNoise> worleyNoiseMap = worleyUtil.GetWorleyNoiseMap(worldPosition);
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
}
