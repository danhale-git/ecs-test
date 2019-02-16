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

    NativeArray<Entity> mapMatrix;

    int squareWidth;

    int matrixWidth;
    int matrixArrayLength;
    int matrixCenterOffset;

    float3 currentMapSquare;
    float3 previousMapSquare;

    float3 currentMatrixRoot;
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

            ComponentType.Create<Tags.GenerateCells>(),
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
    }

    protected override void OnDestroyManager()
    {
        if (mapMatrix.IsCreated) mapMatrix.Dispose ();
    }

    protected override void OnUpdate()
    {
        //  Update current positions
        currentMapSquare    = CurrentMapSquare();
        currentMatrixRoot   = MatrixRoot();

        //  Player moved to a different square
        if(!currentMapSquare.Equals(previousMapSquare))
        {
            //  Reset matrix array
            if(mapMatrix.IsCreated) mapMatrix.Dispose();
            mapMatrix = new NativeArray<Entity>(matrixArrayLength, Allocator.Persistent);

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
        this.previousMatrixRoot = currentMatrixRoot;
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

                bool inPreviousRadius   = SquareInMatrix(position, previousMatrixRoot);
                bool inCurrentRadius    = SquareInMatrix(position, currentMatrixRoot);

                //  Square already exists and is in current view radius
                if(inCurrentRadius)
                {
                    //  Index in flattened matrix
		            float3 index    = IndexInCurrentMatrix(position);
                    int flatIndex   = Util.Flatten2D(index.x, index.z, matrixWidth);

                    //  Add map square in matrices
                    mapMatrix[flatIndex]    = entity;
                    doNotCreate[flatIndex]  = 1;

                    //  Update map square buffer type
                    UpdateBuffer(entity, GetBuffer(IndexInCurrentMatrix(position)), commandBuffer);

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
        for(int i = 0; i < mapMatrix.Length; i++)
        {
            if(doNotCreate[i] == 1)
                continue;

            float3      matrixIndex   = Util.Unflatten2D(i, matrixWidth);
            MapBuffer   buffer        = GetBuffer(matrixIndex);
            float3      worldPosition = (matrixIndex * squareWidth) + currentMatrixRoot;

            Entity entity = entityManager.CreateEntity(mapSquareArchetype);
		    entityManager.SetComponentData<Position>(entity, new Position{ Value = worldPosition } );
            
            mapMatrix[i] = entity;

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

    void RemoveMapSquare(Entity entity)
    {
        float3 position = entityManager.GetComponentData<Position>(entity).Value;

        NativeArray<float3> cardinalDirections = Util.CardinalDirections(Allocator.TempJob);
        for(int i = 0; i < cardinalDirections.Length; i++)
        {
            float3 adjacentPosition = position + (cardinalDirections[i] * squareWidth);

            //  Adjacent square is in active radius
            if(SquareInMatrix(adjacentPosition, currentMatrixRoot))
            {
                Entity adjacent = GetMapSquareFromMatrix(adjacentPosition);

                //  Update AdjacentSquares component when out of Edge buffer   
                if(!entityManager.HasComponent<Tags.GetAdjacentSquares>(adjacent))
                    entityManager.AddComponent(adjacent, typeof(Tags.GetAdjacentSquares));
            }
        }

        cardinalDirections.Dispose();
        entityManager.AddComponent(entity, typeof(Tags.RemoveMapSquare));
    }

    MapBuffer GetBuffer(float3 index)
    {
        if      (SquareInRing(index, 0)) return MapBuffer.EDGE;
        else if (SquareInRing(index, 1)) return MapBuffer.OUTER;
        else if (SquareInRing(index, 2)) return MapBuffer.INNER;
        else return MapBuffer.NONE;
    }

    bool SquareInRing(float3 index, int offset = 0)
	{
        int arrayWidth = matrixWidth-1;

		if(	index.x == offset ||
            index.z == offset ||
			index.x ==  arrayWidth - offset ||
            index.z ==  arrayWidth - offset )
			return true;
		else
			return false;
	}

    bool SquareInMatrix(float3 worldPosition, float3 matrixRootPosition, int offset = 0)
	{
        float3 index = (worldPosition - matrixRootPosition) / squareWidth;
        int arrayWidth = matrixWidth-1;

		if(	index.x >= offset && index.x <= arrayWidth-offset &&
			index.z >= offset && index.z <= arrayWidth-offset )
			return true;
		else
			return false;
	}

    float3 IndexInCurrentMatrix(float3 worldPosition)
    {
        return (worldPosition - currentMatrixRoot) / squareWidth;
    }

    public Entity GetMapSquareFromMatrix(float3 worldPosition)
	{
		float3 index = IndexInCurrentMatrix(worldPosition);
		return mapMatrix[Util.Flatten2D(index.x, index.z, matrixWidth)];
	}

    public bool TryGetMapSquareFromMatrix(float3 worldPosition, out Entity entity)
	{
        float3 localPosition = worldPosition - currentMatrixRoot;
        if(!Util.EdgeOverlap(localPosition, matrixWidth).Equals(float3.zero))
        {
            entity = new Entity();
            return false;
        }

		float3 index = IndexInCurrentMatrix(worldPosition);
		entity = mapMatrix[Util.Flatten2D(index.x, index.z, matrixWidth)];
        return true;
	}
}
