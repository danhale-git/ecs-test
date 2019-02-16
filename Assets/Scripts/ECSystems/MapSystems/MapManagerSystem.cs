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

	public static Entity playerEntity;

    public Matrix<Entity> mapMatrix;

    int squareWidth;

    int matrixWidth;
    int matrixArrayLength;
    int matrixCenterOffset;

    float3 currentMapSquare;
    float3 previousMapSquare;

    float3 previousMatrixRoot;

    EntityArchetype mapSquareArchetype;

    ComponentGroup allSquaresGroup;

	protected override void OnCreateManager()
    {
		entityManager   = World.Active.GetOrCreateManager<EntityManager>();
        squareWidth     = TerrainSettings.mapSquareWidth;

        matrixWidth         = (TerrainSettings.viewDistance * 2) + 1;
        matrixCenterOffset  = TerrainSettings.viewDistance;
        matrixArrayLength   = (int)math.pow(matrixWidth, 2);

        mapSquareArchetype = entityManager.CreateArchetype(
            ComponentType.Create<Position>(),
            ComponentType.Create<RenderMeshComponent>(),
            ComponentType.Create<MapSquare>(),
            ComponentType.Create<WorleyNoise>(),
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
        //  Set previous square and matrix root position to != current
        float3 offset = (new float3(100, 100, 100) * squareWidth);

        previousMapSquare   = CurrentMapSquare()    + offset;
        previousMatrixRoot  = MatrixRoot()          + offset;

        mapMatrix = new Matrix<Entity>(
            MatrixRoot(),
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

        if(!currentMapSquare.Equals(previousMapSquare))
        {
            //  Reset matrix array
            if(mapMatrix.IsCreated()) mapMatrix.Dispose();
            mapMatrix.Create(Allocator.Persistent);

            mapMatrix.rootPosition = MatrixRoot();

            //  List of entities to remove
            NativeList<Entity> squaresToRemove;

            //  Matrix showing already created squares
            NativeArray<int> doNotCreate;
            
            //  Check all map square entities, update and list for removal accordingly
            CheckExistingSquares(out squaresToRemove, out doNotCreate);

            //  Create non-existent map squares in view radius
            CreateNewSquares(doNotCreate);

            //  Action map square removal
            for(int i = 0; i < squaresToRemove.Length; i++)
                RemoveMapSquare(squaresToRemove[i]);

            squaresToRemove.Dispose();
            doNotCreate.Dispose();
        }

        this.previousMapSquare = currentMapSquare;
        this.previousMatrixRoot = mapMatrix.rootPosition;
    }

    float3 MatrixRoot()
    {
        int offset = matrixCenterOffset * squareWidth;
        return new float3(currentMapSquare.x - offset, 0, currentMapSquare.z - offset);
    }

    float3 CurrentMapSquare()
    {
        float3 playerPosition = entityManager.GetComponentData<Position>(playerEntity).Value;
        return Util.VoxelOwner(playerPosition, squareWidth);
    }

    void CheckExistingSquares(out NativeList<Entity> toRemove, out NativeArray<int> doNotCreate)
	{
        EntityCommandBuffer         commandBuffer   = new EntityCommandBuffer(Allocator.Temp);
		NativeArray<ArchetypeChunk> chunks          = allSquaresGroup.CreateArchetypeChunkArray(Allocator.Persistent);	

		ArchetypeChunkEntityType                entityType	    = GetArchetypeChunkEntityType();
		ArchetypeChunkComponentType<Position>   positionType    = GetArchetypeChunkComponentType<Position>(true);
		
        toRemove    = new NativeList<Entity>(Allocator.TempJob);
        doNotCreate = new NativeArray<int>(matrixArrayLength, Allocator.TempJob);

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

                bool inPreviousRadius   = mapMatrix.PositionInWorldBounds(position, previousMatrixRoot);
                bool inCurrentRadius    = mapMatrix.PositionInWorldBounds(position);

                //  Square already exists and is in current view radius
                if(inCurrentRadius)
                {
                    //  Index in flattened matrix
                    int flatIndex   = mapMatrix.WorldPositionToIndex(position);

                    //  Add map square in matrices
                    mapMatrix.SetItem(entity, flatIndex);
                    doNotCreate[flatIndex]  = 1;

                    //  Update map square buffer type
                    UpdateBuffer(entity, GetBuffer(mapMatrix.WorldToMatrixPosition(position)), commandBuffer);

                    squareCount++;
                }
                else
                {
                    //  Delete map square
                    toRemove.Add(entity);
                }
			}

            CustomDebugTools.SetDebugText("Square count", squareCount);
		}

        commandBuffer.Playback(entityManager);
		commandBuffer.Dispose();

		chunks.Dispose();
	}

    //	Check if buffer type needs updating
	void UpdateBuffer(Entity entity, MapBuffer buffer, EntityCommandBuffer commandBuffer)
	{
		switch(buffer)
		{
			//	Outer/None buffer changed to inner buffer
			case MapBuffer.INNER:
				if(entityManager.HasComponent<Tags.OuterBuffer>(entity))
				{
					commandBuffer.RemoveComponent<Tags.OuterBuffer>(entity);
					commandBuffer.AddComponent<Tags.InnerBuffer>(entity, new Tags.InnerBuffer());
				}
                else if(!entityManager.HasComponent<Tags.InnerBuffer>(entity))
                {
					commandBuffer.AddComponent<Tags.InnerBuffer>(entity, new Tags.InnerBuffer());
                }
				break;

			//	Edge/Inner buffer changed to outer buffer
			case MapBuffer.OUTER:
				if(entityManager.HasComponent<Tags.EdgeBuffer>(entity))
				{
					commandBuffer.RemoveComponent<Tags.EdgeBuffer>(entity);
					commandBuffer.AddComponent<Tags.OuterBuffer>(entity, new Tags.OuterBuffer());
				}
                else if(entityManager.HasComponent<Tags.InnerBuffer>(entity))
				{
					commandBuffer.RemoveComponent<Tags.InnerBuffer>(entity);
					commandBuffer.AddComponent<Tags.OuterBuffer>(entity, new Tags.OuterBuffer());
				}
				break;

			//	Outer buffer changed to edge buffer
			case MapBuffer.EDGE:
                if(entityManager.HasComponent<Tags.OuterBuffer>(entity))
				{
					commandBuffer.RemoveComponent<Tags.OuterBuffer>(entity);
					commandBuffer.AddComponent<Tags.EdgeBuffer>(entity, new Tags.EdgeBuffer());
				}
                break;
			
			//	Not a buffer
			default:
				if(entityManager.HasComponent<Tags.EdgeBuffer>(entity))
					commandBuffer.RemoveComponent<Tags.EdgeBuffer>(entity);
				if(entityManager.HasComponent<Tags.InnerBuffer>(entity))
					commandBuffer.RemoveComponent<Tags.InnerBuffer>(entity);
				break;
		}

        CustomDebugTools.HorizontalBufferDebug(entity, (int)buffer);
	}

    void CreateNewSquares(NativeArray<int> doNotCreate)
    {
        for(int i = 0; i < mapMatrix.Length(); i++)
        {
            if(doNotCreate[i] == 1)
                continue;

            float3      matrixPosition  = mapMatrix.IndexToPosition(i);
            MapBuffer   buffer          = GetBuffer(matrixPosition);
            float3      worldPosition   = mapMatrix.MatrixToWorldPosition(matrixPosition);

            Entity entity = entityManager.CreateEntity(mapSquareArchetype);
		    entityManager.SetComponentData<Position>(entity, new Position{ Value = worldPosition } );

            GetWorleyNoise(entity, worldPosition);
            
            mapMatrix.SetItem(entity, i);

            switch(buffer)
            {
                //	Is inner buffer
                case MapBuffer.INNER:
                    entityManager.AddComponent(entity, typeof(Tags.InnerBuffer));
                    break;

                //	Is outer buffer
                case MapBuffer.OUTER:
                    entityManager.AddComponent(entity, typeof(Tags.OuterBuffer));
                    break;

                //	Is edge buffer
                case MapBuffer.EDGE:
                    entityManager.AddComponent(entity, typeof(Tags.EdgeBuffer));
                    break;
                
                //	Is not a buffer
                default:
                    break;
            }

            CustomDebugTools.HorizontalBufferDebug(entity, (int)buffer);
        }
    }

    void GetWorleyNoise(Entity entity, float3 position)
    {
        DynamicBuffer<WorleyNoise> worleyNoiseBuffer = entityManager.GetBuffer<WorleyNoise>(entity);
        worleyNoiseBuffer.ResizeUninitialized(0);

        NativeArray<WorleyNoise> worleyNoiseMap = CellularDistanceToEdge(position, TerrainSettings.cellFrequency);

        worleyNoiseBuffer.AddRange(worleyNoiseMap);
        worleyNoiseMap.Dispose();
    }

    NativeArray<WorleyNoise> CellularDistanceToEdge(float3 position, float frequency = 0.01f)
    {
        NativeArray<WorleyNoise> worleyNoiseMap = new NativeArray<WorleyNoise>((int)math.pow(squareWidth, 2), Allocator.TempJob);

        WorleyNoiseJob cellJob = new WorleyNoiseJob(){
            worleyNoiseMap 	= worleyNoiseMap,						//	Flattened 2D array of noise
			offset 		    = position,						        //	World position of this map square's local 0,0
			squareWidth	    = squareWidth,						    //	Length of one side of a square/cube	
            seed 		    = TerrainSettings.seed,			        //	Perlin noise seed
            frequency 	    = frequency,	                        //	Perlin noise frequency
            perterbAmp      = TerrainSettings.cellEdgeSmoothing,    //  Gradient Peturb amount
			util 		    = new JobUtil(),				        //	Utilities
            noise 		    = new WorleyNoiseGenerator(0)	        //	FastNoise.GetSimplex adapted for Jobs
            };

        cellJob.Schedule(worleyNoiseMap.Length, 16).Complete();

        cellJob.noise.Dispose();

        return worleyNoiseMap;
    }

    void RemoveMapSquare(Entity entity)
    {
        float3 position = entityManager.GetComponentData<Position>(entity).Value;

        UpdateNeighbouringSquares(position);
        
        entityManager.AddComponent(entity, typeof(Tags.RemoveMapSquare));
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
        //  Adjacent square is in active radius
        if(mapMatrix.PositionInWorldBounds(mapSquarePosition))
        {
            Entity adjacent = mapMatrix.GetFromWorldPosition(mapSquarePosition);

            //  Update AdjacentSquares component when out of Edge buffer   
            if(!entityManager.HasComponent<Tags.GetAdjacentSquares>(adjacent))
                entityManager.AddComponent(adjacent, typeof(Tags.GetAdjacentSquares));
        }
    }

    MapBuffer GetBuffer(float3 index)
    {
        if      (mapMatrix.PositionIsInRing(index, 0)) return MapBuffer.EDGE;
        else if (mapMatrix.PositionIsInRing(index, 1)) return MapBuffer.OUTER;
        else if (mapMatrix.PositionIsInRing(index, 2)) return MapBuffer.INNER;
        else return MapBuffer.NONE;
    }
}
