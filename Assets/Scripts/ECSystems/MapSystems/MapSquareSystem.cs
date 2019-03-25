using UnityEngine;
using Unity.Jobs;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Transforms;
using Unity.Rendering;
using MyComponents;

[AlwaysUpdateSystem]
[UpdateInGroup(typeof(MapUpdateGroups.InitialiseSquaresGroup))]
public class MapSquareSystem : ComponentSystem
{
    EntityManager entityManager;

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

        worleyNoiseGen = new WorleyNoiseGenerator(
            TerrainSettings.seed,
            TerrainSettings.cellFrequency,
            TerrainSettings.cellEdgeSmoothing,
            TerrainSettings.cellularJitter
        );

        squareWidth = TerrainSettings.mapSquareWidth;

        mapSquareArchetype = entityManager.CreateArchetype(
            ComponentType.ReadWrite<Translation>(),
            ComponentType.ReadWrite<RenderMeshProxy>(),
            ComponentType.ReadWrite<MapSquare>(),
            ComponentType.ReadWrite<WorleyNoise>(),
            ComponentType.ReadWrite<WorleyCell>(),
            ComponentType.ReadWrite<Block>(),

            ComponentType.ReadWrite<Tags.EdgeBuffer>(),
            
            ComponentType.ReadWrite<Tags.GenerateWorleyNoise>(),
            ComponentType.ReadWrite<Tags.SetHorizontalDrawBounds>(),            
            ComponentType.ReadWrite<Tags.LoadChanges>(),
            ComponentType.ReadWrite<Tags.SetVerticalDrawBounds>(),
            ComponentType.ReadWrite<Tags.GenerateBlocks>(),
            ComponentType.ReadWrite<Tags.SetSlopes>(),
			ComponentType.ReadWrite<Tags.DrawMesh>(),

            ComponentType.ReadWrite<LocalToWorld>()
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
        float3 playerPosition = entityManager.GetComponentData<Translation>(playerEntity).Value;
        return Util.VoxelOwner(playerPosition, squareWidth);
    }

    public int2 CurrentCellIndex()
    {
        float3 voxel = Util.Float3Round(entityManager.GetComponentData<Translation>(playerEntity).Value);
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

        DebugTools.IncrementDebugCount("map square changed");

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

        DebugTools.IncrementDebugCount("call changed");
    }

    void CreateNewSquares()
    {
        NativeArray<ArchetypeChunk> chunks = squaresToCreateGroup.CreateArchetypeChunkArray(Allocator.Persistent);

        ArchetypeChunkEntityType entityType = GetArchetypeChunkEntityType();
        ArchetypeChunkComponentType<Translation> positionType = GetArchetypeChunkComponentType<Translation>();

        NativeList<Entity> entityList = new NativeList<Entity>(Allocator.TempJob);
        NativeList<float3> positionList = new NativeList<float3>(Allocator.TempJob);

        for(int c = 0; c < chunks.Length; c++)
        {
            ArchetypeChunk chunk = chunks[c];

            NativeArray<Entity> entities = chunk.GetNativeArray(entityType);
            NativeArray<Translation> positions = chunk.GetNativeArray(positionType);

            for(int e = 0; e < entities.Length; e++)
            {
                entityList.Add(entities[e]);
                positionList.Add(positions[e].Value);
            }
        }

        chunks.Dispose();

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
        DebugTools.IncrementDebugCount("created");

        Entity entity = entityManager.CreateEntity(mapSquareArchetype);
		entityManager.SetComponentData<Translation>(entity, new Translation{ Value = worldPosition } );

        mapMatrix.AddItem(entity, worldPosition);

        return entity;
    }
}
