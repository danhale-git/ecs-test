using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Transforms;
using MyComponents;

[UpdateAfter(typeof(MapCellDiscoverySystem))]
public class DiscoveryBarrier : EntityCommandBufferSystem { }

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
            cellBufferArray = GetBufferFromEntity<WorleyCell>(),
            currentCellIndex = MapSquareSystem.currentCellIndex,
            squareWidth = squareWidth
        };

        JobHandle discoveryJobHandle = discoveryJob.Schedule(this, inputDependencies);
        discoveryBarrier.AddJobHandleForProducer(discoveryJobHandle);

        return discoveryJobHandle;
    }

    [ExcludeComponent(typeof(Tags.AllCellsDiscovered), typeof(Tags.GenerateWorleyNoise))]
    public struct DiscoveryJob : IJobProcessComponentDataWithEntity<MapSquare>
    {
        public EntityCommandBuffer.Concurrent commandBuffer;

        [NativeDisableParallelForRestriction]
        public BufferFromEntity<WorleyCell> cellBufferArray;

        [ReadOnly] public int2 currentCellIndex;
        [ReadOnly] public int squareWidth;

        public void Execute(Entity entity, int jobIndex, ref MapSquare mapSquare)
        {
            DynamicBuffer<WorleyCell> uniqueCells = cellBufferArray[entity];

            int totalDiscovered = 0;
            int newlyDiscovered = 0;

            for(int i = 0; i < uniqueCells.Length; i++)
                if(DistancFromPlayerCell(uniqueCells[i].index) < TerrainSettings.cellGenerateDistance)
                {
                    totalDiscovered++;

                    if(uniqueCells[i].discovered == 0)
                    {
                        newlyDiscovered++;
                        MarkCellAsDiscovered(uniqueCells, i);
                    }
                }

            if(totalDiscovered == uniqueCells.Length)
                commandBuffer.AddComponent<Tags.AllCellsDiscovered>(jobIndex, entity, new Tags.AllCellsDiscovered());

            if(newlyDiscovered > 0)
                commandBuffer.AddComponent(jobIndex, entity, new Tags.CreateAdjacentSquares());
        }

        void MarkCellAsDiscovered(DynamicBuffer<WorleyCell> uniqueCells, int index)
        {
            WorleyCell cell = uniqueCells[index];
            cell.discovered = 1;
            uniqueCells[index] = cell;
        }

        float DistancFromPlayerCell(int2 cell)
        {
            int2 vector = cell - currentCellIndex;
            float magnitude = math.abs(math.sqrt(vector.x*vector.x + vector.y*vector.y));
            return magnitude;
        }
    }
}
