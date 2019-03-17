using UnityEngine;
using Unity.Jobs;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Transforms;
using Unity.Rendering;
using MyComponents;

[AlwaysUpdateSystem]
public class MapSquareSystem : ComponentSystem
{
    EntityManager entityManager;

    EntityUtil entityUtil;
    WorleyNoiseGenerator worleyNoiseGen;

    int squareWidth;

    public static Entity playerEntity;
    public Matrix<Entity> mapMatrix;

    public static float3 currentMapSquare;
    public static bool mapSquareChanged;
    float3 previousMapSquare;

    public static int2 currentCellIndex;
    public static bool cellChanged;
    int2 previousCellIndex;

    EntityArchetype mapSquareArchetype;
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
            ComponentType.Create<Tags.SetHorizontalDrawBuffer>(),            
            ComponentType.Create<Tags.GenerateTerrain>(),
            ComponentType.Create<Tags.GetAdjacentSquares>(),
            ComponentType.Create<Tags.LoadChanges>(),
            ComponentType.Create<Tags.SetDrawBuffer>(),
            ComponentType.Create<Tags.SetBlockBuffer>(),
            ComponentType.Create<Tags.GenerateBlocks>(),
            ComponentType.Create<Tags.SetSlopes>(),
			ComponentType.Create<Tags.DrawMesh>()
		);

        EntityArchetypeQuery squareToCreateQuery = new EntityArchetypeQuery{
            All = new ComponentType[] { typeof(Tags.CreateAdjacentSquares) }
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
        currentCellIndex = CurrentCellIndex();

        mapMatrix = new Matrix<Entity>(
            1,
            Allocator.Persistent,
            currentMapSquare,
            squareWidth
        );

        CreateMapSquareEntity(currentMapSquare);

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

    protected override void OnUpdate()
    {
        CreateNewSquares();

        currentMapSquare = CurrentMapSquare();
        if(currentMapSquare.Equals(previousMapSquare))
        {
            if(mapSquareChanged) mapSquareChanged = false;
            return;
        }
        else
        {
            mapSquareChanged = true;
            previousMapSquare = currentMapSquare;
        }

        currentCellIndex = CurrentCellIndex();
        if(currentCellIndex.Equals(previousCellIndex))
        {
            if(cellChanged) cellChanged = false;
            return;
        }
        else
        {
            cellChanged = true;
            previousCellIndex = currentCellIndex;
        }
    }

    void CreateNewSquares()
    {
        NativeArray<ArchetypeChunk> chunks = squaresToCreateGroup.CreateArchetypeChunkArray(Allocator.Persistent);

        ArchetypeChunkEntityType entityType = GetArchetypeChunkEntityType();
        ArchetypeChunkComponentType<Position> positionType = GetArchetypeChunkComponentType<Position>();

        NativeList<Entity> entityList = new NativeList<Entity>(Allocator.TempJob);
        NativeList<float3> positionList = new NativeList<float3>(Allocator.TempJob);

        for(int c = 0; c < chunks.Length; c++)
        {
            ArchetypeChunk chunk = chunks[c];

            NativeArray<Entity> entities = chunk.GetNativeArray(entityType);
            NativeArray<Position> positions = chunk.GetNativeArray(positionType);

            for(int e = 0; e < entities.Length; e++)
            {
                entityList.Add(entities[e]);
                positionList.Add(positions[e].Value);
            }
        }

        NativeArray<float3> directions = Util.CardinalDirections(Allocator.Temp);
        
        for(int i = 0; i < entityList.Length; i++)
        {
            if(entityManager.HasComponent<Tags.CreateAdjacentSquares>(entityList[i]))
                entityManager.RemoveComponent<Tags.CreateAdjacentSquares>(entityList[i]);

            for(int d = 0; d < 8; d++)
            {
                float3 adjacentPosition = positionList[i] + (directions[d] * squareWidth);
                if(!mapMatrix.ItemIsSet(adjacentPosition))
                {
                    CustomDebugTools.IncrementDebugCount("squares created");
                    CreateMapSquareEntity(adjacentPosition);
                }
            }
        }

        directions.Dispose();
        entityList.Dispose();
        positionList.Dispose();
    }

    Entity CreateMapSquareEntity(float3 worldPosition)
    {
        Entity entity = entityManager.CreateEntity(mapSquareArchetype);
		entityManager.SetComponentData<Position>(entity, new Position{ Value = worldPosition } );

        mapMatrix.AddItem(entity, worldPosition);

        return entity;
    }
}
