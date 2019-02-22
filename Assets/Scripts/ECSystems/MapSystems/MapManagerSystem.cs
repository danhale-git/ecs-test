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
            ComponentType.Create<UniqueWorleyCells>(),
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

        int matrixWidth = 1;

        mapMatrix = new WorldGridMatrix<Entity>{
            rootPosition = MapMatrixRootPosition(),
            itemWorldSize = squareWidth,
            width = matrixWidth,
            label = Allocator.Persistent
        };

        float3 currentSquare = CurrentMapSquare();
        mapMatrix.ReInitialise(currentSquare);
        DynamicBuffer<UniqueWorleyCells> cellsToDiscover = entityManager.GetBuffer<UniqueWorleyCells>(NewMapSquare(currentSquare));
        NativeArray<float3> startPoints = new NativeArray<float3>(cellsToDiscover.Length, Allocator.TempJob);
        for(int i = 0; i < startPoints.Length; i++)
        {
            startPoints[i] = currentSquare;
        }

        DiscoverNearbyCells(currentSquare, startPoints, cellsToDiscover.AsNativeArray());
    }

    protected override void OnDestroyManager()
    {
        mapMatrix.Dispose();
    }

    /*protected override void OnUpdate()
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
    } */

    protected override void OnUpdate()
    {

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

    MapBuffer GetBuffer(float3 positionInMatrix)
    {
        float3 centerPosition = mapMatrix.WorldToMatrixPosition(currentMapSquare);
        int view = TerrainSettings.viewDistance;

        if      (mapMatrix.IsOffsetFromPosition(positionInMatrix, centerPosition, view)) return MapBuffer.EDGE;        
        else if (mapMatrix.IsOffsetFromPosition(positionInMatrix, centerPosition, view-1)) return MapBuffer.OUTER;
        else if (mapMatrix.IsOffsetFromPosition(positionInMatrix, centerPosition, view-2)) return MapBuffer.INNER;
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

                bool inCurrentRadius = mapMatrix.InDistanceFromWorldPosition(position, currentMapSquare, TerrainSettings.viewDistance);
                
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

    void DiscoverNearbyCells(float3 playerPosition, NativeArray<float3> startSquares, NativeArray<UniqueWorleyCells> cellsToExpore)
    {
        for(int c = 0; c < cellsToExpore.Length; c++)
        {
            //  Cell and corresponding starting square position
            UniqueWorleyCells cell = cellsToExpore[c];
            float3 startSquare = startSquares[c];

            //  Check if cell out of range
            float3 difference = cell.position - playerPosition;
            if(math.sign(difference.x) > 70 || math.sign(difference.z) > 70)
                continue;

            //  Initialise positionsTpCheck with starting position
            NativeList<float3> positionsToCheck = new NativeList<float3>(Allocator.TempJob);
            positionsToCheck.Add(startSquare);

            int safety = 0;
            while(safety < 100)
            {
                safety++;

                if(positionsToCheck.Length == 0) break;

                for(int p = 0; p < positionsToCheck.Length; p++)
                {
                    float3 currentPosition = positionsToCheck[p];

                    CustomDebugTools.MarkError(currentPosition, Color.green);

                    NativeArray<float3> directions = Util.CardinalDirections(Allocator.Temp);
                    for(int i = 0; i < 4; i++)
                    {
                        float3 checkPosition = currentPosition + (directions[i] * squareWidth);

                        Entity entity;

                        if(!mapMatrix.TryGetItemFromWorldPosition(checkPosition, out entity))
                        {
                            entity = NewMapSquare(checkPosition);
                            DynamicBuffer<UniqueWorleyCells> uniqueCells = entityManager.GetBuffer<UniqueWorleyCells>(entity);

                            //  Contains only this cell type
                            if(uniqueCells.Length == 1 && uniqueCells[0].value == cell.value)
                            {
                                positionsToCheck.Add(checkPosition);

                            }

                            //  Add extra discovered cells
                            //  SetItemAndResizeIfNeeded/CheckAndResizeMatrix needs debugging!
                        }
                    }
                }
            }
        }
    }

    Entity NewMapSquare(float3 worldPosition)
    {
        CustomDebugTools.IncrementDebugCount("squares created");

        float3      matrixPosition  = mapMatrix.WorldToMatrixPosition(worldPosition);
        MapBuffer   buffer          = GetBuffer(matrixPosition);
        int matrixIndex = mapMatrix.MatrixPositionToIndex(matrixPosition);

        Entity entity = CreateMapSquareAtPosition(worldPosition);

        DynamicBuffer<WorleyNoise> worleyNoiseBuffer = GenerateWorleyNoise(entity, worldPosition);

        DynamicBuffer<UniqueWorleyCells> uniqueWorleyCells = GetWorleySet(entity, worleyNoiseBuffer);
        
        mapMatrix.SetItemAndResizeIfNeeded(entity, worldPosition);

        SetMapBuffer(entity, buffer);
        Debug.Log(buffer);

        return entity;
    }

    /*void CreateMapSquares(NativeArray<int> alreadyExists)
    {
        for(int i = 0; i < mapMatrix.Length(); i++)
        {
            if(alreadyExists[i] == 1)
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

        DynamicBuffer<WorleyNoise> worleyNoiseBuffer = GenerateWorleyNoise(entity, worldPosition);

        DynamicBuffer<UniqueWorleyCells> worleyCellSet = GetWorleySet(entity, worleyNoiseBuffer);
        
        mapMatrix.SetItem(entity, matrixIndex);

        SetMapBuffer(entity, buffer);
    }  */

    Entity CreateMapSquareAtPosition(float3 worldPosition)
    {
        Entity entity = entityManager.CreateEntity(mapSquareArchetype);
		entityManager.SetComponentData<Position>(entity, new Position{ Value = worldPosition } );
        return entity;
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

    DynamicBuffer<UniqueWorleyCells> GetWorleySet(Entity entity, DynamicBuffer<WorleyNoise> worleyNoiseBuffer)
    {
        NativeArray<WorleyNoise> sortedWorleyNoise = SortedWorleyNoise(worleyNoiseBuffer);

        DynamicBuffer<UniqueWorleyCells> uniqueCellsBuffer = entityManager.GetBuffer<UniqueWorleyCells>(entity);
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
    
    void AddCellToSet(DynamicBuffer<UniqueWorleyCells> uniqueCells, WorleyNoise worleyNoise)
    {
        UniqueWorleyCells setItem = new UniqueWorleyCells {
            value = worleyNoise.currentCellValue,
            index = worleyNoise.currentCellIndex,
            position = worleyNoise.currentCellPosition
        };
        uniqueCells.Add(setItem);
    }

    void SetMapBuffer(Entity entity, MapBuffer buffer)
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
        if(mapMatrix.WorldPositionIsInMatrix(mapSquarePosition))
        {
            Entity adjacent = mapMatrix.GetItemFromWorldPosition(mapSquarePosition);

            tags.TryAddTag<Tags.GetAdjacentSquares>(adjacent);
        }
    }
}
