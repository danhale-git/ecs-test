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

    public Matrix<Entity> mapMatrix;

    public float3 currentMapSquare;
    float3 previousMapSquare;
    public int2 currentCellIndex;
    int2 previousCellIndex;

    EntityArchetype mapSquareArchetype;
    EntityArchetype worleyCellArchetype;

    ComponentGroup squaresToCreateGroup;

	protected override void OnCreateManager()
    {
		entityManager = World.Active.GetOrCreateManager<EntityManager>();

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

            ComponentType.Create<Tags.GenerateWorleyNoise>(),
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

        EntityArchetypeQuery squareToCreateQuery = new EntityArchetypeQuery{
            All = new ComponentType[] { typeof(SquareToCreate) }
        };
        squaresToCreateGroup = GetComponentGroup(squareToCreateQuery);
    }

    protected override void OnDestroyManager()
    {
        mapMatrix.Dispose();
    }
    
    protected override void OnStartRunning()
    {
        currentMapSquare = CurrentMapSquare();
        InitialiseMapMatrix(currentMapSquare);

        CreateMapSquareEntity(currentMapSquare);
        currentCellIndex = CurrentCellIndex();

        //  Initialise 'previous' variables to somomething that doesn't match the current position
        previousMapSquare = currentMapSquare + (100 * squareWidth);
        previousCellIndex = currentCellIndex + 100;
    }

    public float3 CurrentMapSquare()
    {
        float3 playerPosition = entityManager.GetComponentData<Position>(playerEntity).Value;
        return Util.VoxelOwner(playerPosition, squareWidth);
    }

    public int2 CurrentCellIndex()
    {
        float3 voxel = Util.Float3Round(entityManager.GetComponentData<Position>(playerEntity).Value);
        return worleyNoiseGen.GetEdgeData(voxel.x, voxel.z).currentCellIndex;
    }

    void InitialiseMapMatrix(float3 rootPosition)
    {
        mapMatrix = new Matrix<Entity>(1, Allocator.Persistent, rootPosition, squareWidth);
    }

    protected override void OnUpdate()
    {
        CreateSquares();

        currentMapSquare = CurrentMapSquare();
        if(currentMapSquare.Equals(previousMapSquare)) return;
        else previousMapSquare = currentMapSquare;

        currentCellIndex = CurrentCellIndex();
        if(currentCellIndex.Equals(previousCellIndex)) return;
        else previousCellIndex = currentCellIndex;
    }

    void CreateSquares()
    {
        NativeArray<ArchetypeChunk> chunks = squaresToCreateGroup.CreateArchetypeChunkArray(Allocator.Persistent);

        ArchetypeChunkEntityType entityType = GetArchetypeChunkEntityType();
        ArchetypeChunkBufferType<SquareToCreate> squaresToCreateBufferType = GetArchetypeChunkBufferType<SquareToCreate>();

        NativeList<Entity> removeComponentList = new NativeList<Entity>(Allocator.TempJob);
        NativeList<float3> createMapSquareList = new NativeList<float3>(Allocator.TempJob);

        for(int c = 0; c < chunks.Length; c++)
        {
            ArchetypeChunk chunk = chunks[c];

            NativeArray<Entity> entities = chunk.GetNativeArray(entityType);
            BufferAccessor<SquareToCreate> squareBuffers = chunk.GetBufferAccessor<SquareToCreate>(squaresToCreateBufferType);

            for(int e = 0; e < entities.Length; e++)
            {
                Entity entity = entities[e];
                DynamicBuffer<SquareToCreate> squaresToCreate = squareBuffers[e];

                for(int i = 0; i < squaresToCreate.Length; i++)
                    createMapSquareList.Add(squaresToCreate[i].squarePosition);

                removeComponentList.Add(entity);
            }
        }

        for(int i = 0; i < removeComponentList.Length; i++)
        {
            if(entityManager.HasComponent<SquareToCreate>(removeComponentList[i]))
                entityManager.RemoveComponent<SquareToCreate>(removeComponentList[i]);
        }

        for(int i = 0; i < createMapSquareList.Length; i++)
        {
            float3 squarePosition = createMapSquareList[i];
            if(!mapMatrix.ItemIsSet(squarePosition))
            {
                CustomDebugTools.IncrementDebugCount("squares created");
                CreateMapSquareEntity(squarePosition);
            }
        }

        removeComponentList.Dispose();
        createMapSquareList.Dispose();
    }


    /*Entity GetOrCreateMapSquare(float3 position)
    {
        Entity mapSquareEntity;
        if(!mapMatrix.array.TryGetItem(position, out mapSquareEntity))
        {
            mapSquareEntity = CreateMapSquareEntity(position);
            GenerateWorleyNoise(mapSquareEntity, position);
        }
        return mapSquareEntity;
    } */

    Entity CreateMapSquareEntity(float3 worldPosition)
    {
        Entity entity = entityManager.CreateEntity(mapSquareArchetype);
		entityManager.SetComponentData<Position>(entity, new Position{ Value = worldPosition } );

        mapMatrix.AddItem(entity, worldPosition);

        return entity;
    }
}
