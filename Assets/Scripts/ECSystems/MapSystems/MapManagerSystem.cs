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
	enum MapBuffer { NONE, INNER, OUTER, EDGE }
    
    EntityManager entityManager;
    TagUtil tags;

    int squareWidth;

	public static Entity playerEntity;

    public WorldGridMatrix<Entity> mapMatrix;

    float3 currentMapSquare;
    float3 previousMapSquare;

    EntityArchetype mapSquareArchetype;
    ComponentGroup allSquaresGroup;

	protected override void OnCreateManager()
    {
		entityManager = World.Active.GetOrCreateManager<EntityManager>();
        tags = new TagUtil(entityManager);
        squareWidth = TerrainSettings.mapSquareWidth;

        mapSquareArchetype = entityManager.CreateArchetype(
            ComponentType.Create<Position>(),
            ComponentType.Create<RenderMeshComponent>(),
            ComponentType.Create<MapSquare>(),
            ComponentType.Create<WorleyNoise>(),
            ComponentType.Create<WorleyCellValueSet>(),
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
    }

    protected override void OnStartRunning()
    {
        //  Initialise previous square as different to starting square
        float3 offset = (new float3(100, 100, 100) * squareWidth);
        previousMapSquare = CurrentMapSquare() + offset;

        int matrixWidth = (TerrainSettings.viewDistance * 2) + 1;

        mapMatrix = new WorldGridMatrix<Entity>(
            MapMatrixRootPosition(),
            squareWidth,
            matrixWidth,
            Allocator.Persistent
        );
    }

    protected override void OnDestroyManager()
    {
        mapMatrix.Dispose();
    }

    protected override void OnUpdate()
    {
        currentMapSquare = CurrentMapSquare();

        if(currentMapSquare.Equals(previousMapSquare))
            return;

        mapMatrix.ReInitialise(MapMatrixRootPosition());

        NativeList<Entity> squaresToRemove;

        NativeArray<int> alreadyExists;
        
        CheckAndUpdateMapSquares(out squaresToRemove, out alreadyExists);

        CreateMapSquares(alreadyExists);
        
        RemoveMapSquares(squaresToRemove);

        squaresToRemove.Dispose();
        alreadyExists.Dispose();

        previousMapSquare = currentMapSquare;
    }

    float3 MapMatrixRootPosition()
    {
        int offset = TerrainSettings.viewDistance * squareWidth;
        return new float3(currentMapSquare.x - offset, 0, currentMapSquare.z - offset);
    }

    float3 CurrentMapSquare()
    {
        float3 playerPosition = entityManager.GetComponentData<Position>(playerEntity).Value;
        return Util.VoxelOwner(playerPosition, squareWidth);
    }

    MapBuffer GetBuffer(float3 index)
    {
        if      (mapMatrix.PositionIsInRing(index, 0)) return MapBuffer.EDGE;
        else if (mapMatrix.PositionIsInRing(index, 1)) return MapBuffer.OUTER;
        else if (mapMatrix.PositionIsInRing(index, 2)) return MapBuffer.INNER;
        else return MapBuffer.NONE;
    }

    void CheckAndUpdateMapSquares(out NativeList<Entity> toRemove, out NativeArray<int> alreadyExists)
	{
        EntityCommandBuffer         commandBuffer   = new EntityCommandBuffer(Allocator.Temp);
		NativeArray<ArchetypeChunk> chunks          = allSquaresGroup.CreateArchetypeChunkArray(Allocator.Persistent);	

		ArchetypeChunkEntityType                entityType	    = GetArchetypeChunkEntityType();
		ArchetypeChunkComponentType<Position>   positionType    = GetArchetypeChunkComponentType<Position>(true);
		
        toRemove    = new NativeList<Entity>(Allocator.TempJob);
        alreadyExists = new NativeArray<int>(mapMatrix.Length(), Allocator.TempJob);

        int squareCount = 0;

		for(int c = 0; c < chunks.Length; c++)
		{
			ArchetypeChunk chunk = chunks[c];

			NativeArray<Entity> entities 	= chunk.GetNativeArray(entityType);
			NativeArray<Position> positions = chunk.GetNativeArray(positionType);

			for(int e = 0; e < entities.Length; e++)
			{
				Entity entity   = entities[e];
				float3 position = positions[e].Value;

                bool inCurrentRadius    = mapMatrix.PositionInWorldBounds(position);

                if(inCurrentRadius)
                {
                    int matrixIndex = mapMatrix.WorldPositionToIndex(position);

                    mapMatrix.SetItem(entity, matrixIndex);
                    alreadyExists[matrixIndex]  = 1;

                    MapBuffer buffer = GetBuffer(mapMatrix.WorldToMatrixPosition(position));
                    UpdateBuffer(entity, buffer, commandBuffer);

                    squareCount++;
                }
                else
                {
                    //toRemove.Add(entity);
                    if(entityManager.HasComponent<RenderMesh>(entity))
                        commandBuffer.RemoveComponent(entity, typeof(RenderMesh));

                    tags.TryAddTag<Tags.DrawMesh>(entity, commandBuffer);
                }
			}

            CustomDebugTools.SetDebugText("Square count", squareCount);
		}

        commandBuffer.Playback(entityManager);
		commandBuffer.Dispose();

		chunks.Dispose();//
	}

	void UpdateBuffer(Entity entity, MapBuffer buffer, EntityCommandBuffer commandBuffer)
	{
		switch(buffer)
		{
			//	Outer/None buffer changed to inner buffer
			case MapBuffer.INNER:
                if(!tags.TryReplaceTag<Tags.OuterBuffer, Tags.InnerBuffer>(entity, commandBuffer))
                    tags.TryAddTag<Tags.InnerBuffer>(entity, commandBuffer);
				break;

			//	Edge/Inner buffer changed to outer buffer
			case MapBuffer.OUTER:
                if(!tags.TryReplaceTag<Tags.EdgeBuffer, Tags.OuterBuffer>(entity, commandBuffer))
                    tags.TryReplaceTag<Tags.InnerBuffer, Tags.OuterBuffer>(entity, commandBuffer);
				break;

			//	Outer buffer changed to edge buffer
			case MapBuffer.EDGE:
                tags.TryReplaceTag<Tags.OuterBuffer, Tags.EdgeBuffer>(entity, commandBuffer);
                break;
			
			//	Not a buffer
			default:
                tags.TryRemoveTag<Tags.EdgeBuffer>(entity, commandBuffer);
                tags.TryRemoveTag<Tags.InnerBuffer>(entity, commandBuffer);
				break;
		}

        CustomDebugTools.HorizontalBufferDebug(entity, (int)buffer);
	}

    void CreateMapSquares(NativeArray<int> doNotCreate)
    {
        for(int i = 0; i < mapMatrix.Length(); i++)
        {
            if(doNotCreate[i] == 1)
                continue;

            NewMapSquare(i);
        }
    }

    void NewMapSquare(int matrixIndex)
    {
        float3      matrixPosition  = mapMatrix.IndexToMatrixPosition(matrixIndex);
        MapBuffer   buffer          = GetBuffer(matrixPosition);
        float3      worldPosition   = mapMatrix.MatrixToWorldPosition(matrixPosition);

        Entity entity = CreateMapSquareAtPosition(worldPosition);

        GenerateWorleyNoise(entity, worldPosition);
        
        mapMatrix.SetItem(entity, matrixIndex);

        SetBuffer(entity, buffer);
    }

    Entity CreateMapSquareAtPosition(float3 worldPosition)
    {
        Entity entity = entityManager.CreateEntity(mapSquareArchetype);
		entityManager.SetComponentData<Position>(entity, new Position{ Value = worldPosition } );
        return entity;
    }

    void GenerateWorleyNoise(Entity entity, float3 position)
    {
        DynamicBuffer<WorleyNoise> worleyNoiseBuffer = entityManager.GetBuffer<WorleyNoise>(entity);
        worleyNoiseBuffer.ResizeUninitialized(0);

        NativeArray<WorleyNoise> worleyNoiseMap = RunWorleyNoiseJob(position, TerrainSettings.cellFrequency);
    
        GetWorleyCellValueSet(entity, worleyNoiseMap);

        worleyNoiseBuffer.AddRange(worleyNoiseMap);
        worleyNoiseMap.Dispose();
    }

    NativeArray<WorleyNoise> RunWorleyNoiseJob(float3 position, float frequency = 0.01f)
    {
        NativeArray<WorleyNoise> worleyNoiseMap = new NativeArray<WorleyNoise>((int)math.pow(squareWidth, 2), Allocator.TempJob);

        WorleyNoiseJob cellJob = new WorleyNoiseJob(){
            worleyNoiseMap 	= worleyNoiseMap,						//	Flattened 2D array of noise
			offset 		    = position,						        //	World position of this map square's local 0,0
			squareWidth	    = squareWidth,						    //	Length of one side of a square/cube	
            seed 		    = TerrainSettings.seed,			        //	Perlin noise seed
            frequency 	    = frequency,	                        //	Perlin noise frequency
            perterbAmp      = TerrainSettings.cellEdgeSmoothing,    //  Gradient Peturb amount
            cellularJitter  = TerrainSettings.cellularJitter,       //  Randomness of cell shapes
			util 		    = new JobUtil(),				        //	Utilities
            noise 		    = new WorleyNoiseGenerator(0)	        //	FastNoise.GetSimplex adapted for Jobs
        };

        cellJob.Schedule(worleyNoiseMap.Length, 16).Complete();

        cellJob.noise.Dispose();

        return worleyNoiseMap;
    }

    void GetWorleyCellValueSet(Entity entity,  NativeArray<WorleyNoise> worleyNoiseMap)
    {
        NativeArray<float> cellValues = SortedCellValues(worleyNoiseMap);
        DynamicBuffer<WorleyCellValueSet> cellValueSetBuffer = entityManager.GetBuffer<WorleyCellValueSet>(entity);

        int setIndex = 0;
        cellValueSetBuffer.Add(new WorleyCellValueSet { value = cellValues[0] });
        for(int i = 1; i < cellValues.Length; i++)
        {
            if(cellValues[i] != cellValueSetBuffer[setIndex].value)
            {
                setIndex++;
                cellValueSetBuffer.Add(new WorleyCellValueSet { value = cellValues[i] });
            }
        }

        cellValues.Dispose();
    }
    NativeArray<float> SortedCellValues(NativeArray<WorleyNoise> worleyNoiseMap)
    {
        NativeArray<float> cellValues = new NativeArray<float>(worleyNoiseMap.Length, Allocator.Temp);
        for(int i = 0; i < cellValues.Length; i++)
            cellValues[i] = worleyNoiseMap[i].currentCellValue;

        cellValues.Sort();        

        return cellValues;
    }

    void SetBuffer(Entity entity, MapBuffer buffer)
    {
        switch(buffer)
        {
            //	Is inner buffer
            case MapBuffer.INNER:
                tags.AddTag<Tags.InnerBuffer>(entity);
                break;

            //	Is outer buffer
            case MapBuffer.OUTER:
                tags.AddTag<Tags.OuterBuffer>(entity);
                break;

            //	Is edge buffer
            case MapBuffer.EDGE:
                tags.AddTag<Tags.EdgeBuffer>(entity);
                break;
            
            //	Is not a buffer
            default:
                break;
        }

        CustomDebugTools.HorizontalBufferDebug(entity, (int)buffer);
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
        if(mapMatrix.PositionInWorldBounds(mapSquarePosition))
        {
            Entity adjacent = mapMatrix.GetItemFromWorldPosition(mapSquarePosition);

            tags.TryAddTag<Tags.GetAdjacentSquares>(adjacent);
        }
    }
}
