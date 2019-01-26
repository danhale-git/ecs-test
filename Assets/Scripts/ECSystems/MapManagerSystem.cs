using UnityEngine;
using Unity.Jobs;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Transforms;
using Unity.Rendering;
using MyComponents;

[AlwaysUpdateSystem]
public class MapManagerSystem : ComponentSystem
{
	enum MapBuffer { NONE, INNER, OUTER, EDGE }
    
    EntityManager entityManager;

	public static Entity playerEntity;

    static NativeArray<Entity> mapMatrix;

    static int cubeSize;

    static  int matrixWidth;
            int matrixArrayLength;
            int matrixCenterOffset;

    float3 currentMapSquare;
    float3 previousMapSquare;

    static  float3 currentMatrixRoot;
            float3 previousMatrixRoot;

    EntityArchetype mapSquareArchetype;

    ComponentGroup allSquaresGroup;

	protected override void OnCreateManager()
    {
		entityManager   = World.Active.GetOrCreateManager<EntityManager>();
        cubeSize        = TerrainSettings.cubeSize;

        matrixWidth         = (TerrainSettings.viewDistance * 2) + 1;
        matrixCenterOffset  = TerrainSettings.viewDistance;
        matrixArrayLength   = (int)math.pow(matrixWidth, 2);

        mapSquareArchetype = entityManager.CreateArchetype(
            ComponentType.Create<Position>(),
            ComponentType.Create<RenderMeshComponent>(),
            ComponentType.Create<MapSquare>(),
            ComponentType.Create<Topology>(),
            ComponentType.Create<Block>(),

            ComponentType.Create<Tags.GenerateTerrain>(),
            ComponentType.Create<Tags.GetAdjacentSquares>(),
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
        float3 offset = (new float3(100, 100, 100) * cubeSize);

        previousMapSquare   = CurrentMapSquare()    + offset;
        previousMatrixRoot  = MatrixRoot()          + offset;
    }

    protected override void OnUpdate()
    {
        //  Reset matrix array
        if(mapMatrix.IsCreated) mapMatrix.Dispose();
        mapMatrix = new NativeArray<Entity>(matrixArrayLength, Allocator.Persistent);

        //  Update current positions
        currentMapSquare    = CurrentMapSquare();
        currentMatrixRoot          = MatrixRoot();

        //  Player moved to a different square
        if(!currentMapSquare.Equals(previousMapSquare))
        {
            CheckExistingSquares();
            CreateNewSquares();
        }

        this.previousMapSquare = currentMapSquare;
        this.previousMatrixRoot = currentMatrixRoot;
    }

    void CheckExistingSquares()
	{
        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);

		ArchetypeChunkEntityType                entityType	    = GetArchetypeChunkEntityType();
		ArchetypeChunkComponentType<Position>   positionType    = GetArchetypeChunkComponentType<Position>();
		
		NativeArray<ArchetypeChunk> chunks = allSquaresGroup.CreateArchetypeChunkArray(Allocator.Persistent);	

		for(int c = 0; c < chunks.Length; c++)
		{
			ArchetypeChunk chunk = chunks[c];

			NativeArray<Entity> entities 	= chunk.GetNativeArray(entityType);
			NativeArray<Position> positions = chunk.GetNativeArray(positionType);

			for(int e = 0; e < entities.Length; e++)
			{
				Entity entity = entities[e];
				float3 position = positions[e].Value;

                bool inPreviousRadius   = SquareInMatrix(position, previousMatrixRoot);
                bool inCurrentRadius    = SquareInMatrix(position, currentMatrixRoot);

                //  Square already exists and is in current view radius
                if(inCurrentRadius)
                {
                    if(inPreviousRadius)
                    {
                        UpdateBuffer(entity, GetBuffer(IndexInCurrentMatrix(position)), commandBuffer);
                        AddMapSquareToMatrix(entity, position);
                    }
                }
			}
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
			//	Outer buffer changed to inner buffer
			case MapBuffer.INNER:
				if(entityManager.HasComponent<Tags.OuterBuffer>(entity))
				{
					commandBuffer.RemoveComponent<Tags.OuterBuffer>(entity);
					commandBuffer.AddComponent<Tags.InnerBuffer>(entity, new Tags.InnerBuffer());
				}	
				break;

			//	Edge buffer changed to outer buffer
			case MapBuffer.OUTER:
				if(entityManager.HasComponent<Tags.EdgeBuffer>(entity))
				{
					commandBuffer.RemoveComponent<Tags.EdgeBuffer>(entity);
					commandBuffer.AddComponent<Tags.OuterBuffer>(entity, new Tags.OuterBuffer());
				}
				break;

			//	Still edge buffer
			case MapBuffer.EDGE: break;
			
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

    void CreateNewSquares()
    {
        for(int i = 0; i < mapMatrix.Length; i++)
        {
            float3      matrixIndex   = Util.Unflatten2D(i, matrixWidth);
            MapBuffer   buffer        = GetBuffer(matrixIndex);
            float3      worldPosition = (matrixIndex * cubeSize) + currentMatrixRoot;

            if(SquareInMatrix(worldPosition, previousMatrixRoot))
                continue;

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
        float3 index = (worldPosition - matrixRootPosition) / cubeSize;
        int arrayWidth = matrixWidth-1;

		if(	index.x >= offset && index.x <= arrayWidth-offset &&
			index.z >= offset && index.z <= arrayWidth-offset )
			return true;
		else
			return false;
	}

    float3 MatrixRoot()
    {
        int offset = matrixCenterOffset * cubeSize;
        return new float3(currentMapSquare.x - offset, 0, currentMapSquare.z - offset);
    }

    float3 CurrentMapSquare()
    {
        float3 playerPosition = entityManager.GetComponentData<Position>(playerEntity).Value;
        return Util.VoxelOwner(playerPosition, cubeSize);
    }

    static float3 IndexInCurrentMatrix(float3 worldPosition)
    {
        return (worldPosition - currentMatrixRoot) / cubeSize;
    }

    void AddMapSquareToMatrix(Entity entity, float3 worldPosition)
	{
		float3 index = IndexInCurrentMatrix(worldPosition);
		mapMatrix[Util.Flatten2D(index.x, index.z, matrixWidth)] = entity;
	}

    public static Entity GetMapSquareFromMatrix(float3 worldPosition)
	{
		float3 index = IndexInCurrentMatrix(worldPosition);
		return mapMatrix[Util.Flatten2D(index.x, index.z, matrixWidth)];
	}
}
