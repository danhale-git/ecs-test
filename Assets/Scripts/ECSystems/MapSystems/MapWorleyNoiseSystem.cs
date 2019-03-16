using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Transforms;
using MyComponents;

[UpdateAfter(typeof(MapCellDiscoverySystem))]
public class WorleyBarrier : BarrierSystem { }

[UpdateAfter(typeof(MapSquareSystem))]
public class MapWorleyNoiseSystem : JobComponentSystem
{
    EntityManager entityManager;

    WorleyNoiseGenerator worleyNoiseGen;

    WorleyBarrier worleyBarrier;

    int squareWidth;

    protected override void OnCreateManager()
    {
        entityManager = World.Active.GetOrCreateManager<EntityManager>();
        worleyBarrier = World.Active.GetOrCreateManager<WorleyBarrier>();
        squareWidth = TerrainSettings.mapSquareWidth;

        worleyNoiseGen = new WorleyNoiseGenerator(
            TerrainSettings.seed,
            TerrainSettings.cellFrequency,
            TerrainSettings.cellEdgeSmoothing,
            TerrainSettings.cellularJitter
        );
    }

    protected override JobHandle OnUpdate(JobHandle inputDependencies)
    {
        WorleyJob worleyJob = new WorleyJob(){
            commandBuffer = worleyBarrier.CreateCommandBuffer().ToConcurrent(),
            worleyNoiseBufferFromEntity = GetBufferFromEntity<WorleyNoise>(),
            uniqueWorleyCellsFromEntity = GetBufferFromEntity<WorleyCell>(),
            squareWidth = squareWidth,
            util = new JobUtil(),
            noise = worleyNoiseGen
        };

        JobHandle worleyHandle = worleyJob.Schedule(this, inputDependencies);

        worleyBarrier.AddJobHandleForProducer(worleyHandle);

        return worleyHandle;
    }

    [RequireComponentTag(typeof(Tags.GenerateWorleyNoise))]
    public struct WorleyJob : IJobProcessComponentDataWithEntity<MapSquare, Position>
    {
        public EntityCommandBuffer.Concurrent commandBuffer;
        
        [NativeDisableParallelForRestriction]
        public BufferFromEntity<WorleyNoise> worleyNoiseBufferFromEntity;
        [NativeDisableParallelForRestriction]        
        public BufferFromEntity<WorleyCell> uniqueWorleyCellsFromEntity;

        [ReadOnly] public int squareWidth;
        [ReadOnly] public JobUtil util;
        [ReadOnly] public WorleyNoiseGenerator noise;

        public void Execute(Entity mapSquareEntity, int jobIndex, ref MapSquare mapSquare, ref Position position)
        {
            NativeArray<WorleyNoise> worleyNoiseMap = GenerateNoiseMap(position.Value);
            worleyNoiseBufferFromEntity[mapSquareEntity].CopyFrom(worleyNoiseMap);

            NativeArray<WorleyCell> worleyCellSet = UniqueWorleyCellSet(worleyNoiseMap);
            uniqueWorleyCellsFromEntity[mapSquareEntity].CopyFrom(worleyCellSet);

            worleyNoiseMap.Dispose();
            worleyCellSet.Dispose();

            commandBuffer.RemoveComponent<Tags.GenerateWorleyNoise>(jobIndex, mapSquareEntity);
        }

        public NativeArray<WorleyNoise> GenerateNoiseMap(float3 offset)
        {
            NativeArray<WorleyNoise> worleyNoiseMap = new NativeArray<WorleyNoise>((int)math.pow(squareWidth, 2), Allocator.Temp);

            for(int i = 0; i < worleyNoiseMap.Length; i++)
            {
                float3 squarePosition = util.Unflatten2D(i, squareWidth) + offset;
                worleyNoiseMap[i] = noise.GetEdgeData(squarePosition.x, squarePosition.z);
            }
            return worleyNoiseMap;
        }

        public NativeArray<WorleyCell> UniqueWorleyCellSet(NativeArray<WorleyNoise> worleyNoiseMap)
        {
            NativeList<WorleyNoise> noiseSet = util.Set<WorleyNoise>(worleyNoiseMap, Allocator.Temp);
            NativeArray<WorleyCell> cellSet = new NativeArray<WorleyCell>(noiseSet.Length, Allocator.Temp);

            for(int i = 0; i < noiseSet.Length; i++)
            {
                WorleyNoise worleyNoise = noiseSet[i];

                WorleyCell cell = new WorleyCell {
                    value = worleyNoise.currentCellValue,
                    index = worleyNoise.currentCellIndex,
                    position = worleyNoise.currentCellPosition
                };

                cellSet[i] = cell;
            }

            return cellSet;
        }
     }
}
