using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Transforms;
using MyComponents;

[UpdateAfter(typeof(MapCellDiscoverySystem))]
public class DiscoveryBarrier : BarrierSystem { }

[UpdateAfter(typeof(MapWorleyNoiseSystem))]
public class MapCellDiscoverySystem : JobComponentSystem
{
    EntityManager entityManager;
    DiscoveryBarrier discoveryBarrier;

    int squareWidth;
    int cellDistance;

    protected override void OnCreateManager()
    {
        entityManager = World.Active.GetOrCreateManager<EntityManager>();
        discoveryBarrier = World.Active.GetOrCreateManager<DiscoveryBarrier>();

        squareWidth = TerrainSettings.mapSquareWidth;
        cellDistance = TerrainSettings.cellGenerateDistance;
    }

    protected override JobHandle OnUpdate(JobHandle inputDependencies)
    {
        if(!MapSquareSystem.cellChanged) return inputDependencies;

        DiscoveryJob discoveryJob = new DiscoveryJob(){
            commandBuffer = discoveryBarrier.CreateCommandBuffer().ToConcurrent(),
            uniqueCellsFromEntity = GetBufferFromEntity<WorleyCell>(),
            currentCellIndex = MapSquareSystem.currentCellIndex,
            squareWidth = squareWidth,
            directions = Util.CardinalDirections(Allocator.TempJob)
        };

        JobHandle discoveryHandle = discoveryJob.Schedule(this, inputDependencies);
        discoveryBarrier.AddJobHandleForProducer(discoveryHandle);

        return discoveryHandle;
    }

    [RequireSubtractiveComponent(typeof(Tags.CellDiscoveryComplete), typeof(Tags.GenerateWorleyNoise))]
    public struct DiscoveryJob : IJobProcessComponentDataWithEntity<MapSquare, Position>
    {
        public EntityCommandBuffer.Concurrent commandBuffer;

        [NativeDisableParallelForRestriction]
        public BufferFromEntity<WorleyCell> uniqueCellsFromEntity;

        [ReadOnly] public int2 currentCellIndex;
        [ReadOnly] public int squareWidth;

        [DeallocateOnJobCompletion]
        [ReadOnly] public NativeArray<float3> directions;

        public void Execute(Entity mapSquareEntity, int jobIndex, ref MapSquare mapSquare, ref Position position)
        {
            DynamicBuffer<WorleyCell> uniqueCells = uniqueCellsFromEntity[mapSquareEntity];
            int newlyDiscovered = DiscoverCells(mapSquareEntity, jobIndex, uniqueCells);

            if(newlyDiscovered > 0)
            {
                DynamicBuffer<SquareToCreate> squaresToCreate = commandBuffer.AddBuffer<SquareToCreate>(jobIndex, mapSquareEntity);
                CreateAdjacentSquares(squaresToCreate, position.Value);
            }
        }

        int DiscoverCells(Entity mapSquareEntity, int jobIndex, DynamicBuffer<WorleyCell> uniqueCells)
        {
            int totalDiscovered = 0;
            int newlyDiscovered = 0;

            for(int i = 0; i < uniqueCells.Length; i++)
                if(Distance(uniqueCells[i].index, currentCellIndex) < TerrainSettings.cellGenerateDistance)
                {
                    totalDiscovered++;

                    if(uniqueCells[i].discovered == 0)
                    {
                        newlyDiscovered++;

                        WorleyCell cell = uniqueCells[i];
                        cell.discovered = 1;
                        uniqueCells[i] = cell;
                    }
                }

            if(totalDiscovered == uniqueCells.Length)
                commandBuffer.AddComponent<Tags.CellDiscoveryComplete>(jobIndex, mapSquareEntity, new Tags.CellDiscoveryComplete());

            return newlyDiscovered; 
        }

        float Distance(int2 distance, int2 from)
        {
            int2 vector = distance - from;
            float magnitude = math.abs(math.sqrt(vector.x*vector.x + vector.y*vector.y));
            return magnitude;
        }

        void CreateAdjacentSquares(DynamicBuffer<SquareToCreate> buffer, float3 position)
        {
            for(int d = 0; d < 8; d++)
            {
                float3 adjacentPosition = position + (directions[d] * squareWidth);
                buffer.Add(new SquareToCreate { squarePosition = adjacentPosition });
            }
        }
    }
}
