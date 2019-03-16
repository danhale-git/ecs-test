﻿using Unity.Entities;
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
    MapCellMarchingSystem managerSystem;

    DiscoveryBarrier discoveryBarrier;

    int squareWidth;
    NativeArray<float3> directions;

    MapMatrix<Entity> mapMatrix;

    protected override void OnCreateManager()
    {
        entityManager = World.Active.GetOrCreateManager<EntityManager>();
        managerSystem = World.Active.GetOrCreateManager<MapCellMarchingSystem>();

        discoveryBarrier = World.Active.GetOrCreateManager<DiscoveryBarrier>();
        squareWidth = TerrainSettings.mapSquareWidth;
        directions = Util.CardinalDirections(Allocator.Persistent);
    }

    protected override JobHandle OnUpdate(JobHandle inputDependencies)
    {
        DiscoveryJob discoveryJob = new DiscoveryJob(){
            commandBuffer = discoveryBarrier.CreateCommandBuffer().ToConcurrent(),
            uniqueCellsFromEntity = GetBufferFromEntity<WorleyCell>(),
            currentCellIndex = managerSystem.currentCellIndex,
            squareWidth = squareWidth,
            directions = directions
        };

        JobHandle discoveryHandle = discoveryJob.Schedule(this, inputDependencies);

        discoveryBarrier.AddJobHandleForProducer(discoveryHandle);

        return discoveryHandle;
    }

    [RequireSubtractiveComponent(typeof(Tags.CellDiscoveryComplete))]
    public struct DiscoveryJob : IJobProcessComponentDataWithEntity<MapSquare, Position>
    {
        public EntityCommandBuffer.Concurrent commandBuffer;

        public BufferFromEntity<WorleyCell> uniqueCellsFromEntity;

        public int2 currentCellIndex;

        public int squareWidth;
        public NativeArray<float3> directions;

        public void Execute(Entity mapSquareEntity, int jobIndex, ref MapSquare mapSquare, ref Position position)
        {
            DynamicBuffer<WorleyCell> uniqueCells = uniqueCellsFromEntity[mapSquareEntity];

            int discoveredCellCount = 0;
            int newlyDiscoveredCellCount = 0;

            for(int i = 0; i < uniqueCells.Length; i++)
            {
                int2 distance = uniqueCells[i].index - currentCellIndex;
                float magnitude = math.abs(math.sqrt(distance.x*distance.x + distance.y*distance.y));
                
                if(magnitude < 2)
                {
                    discoveredCellCount++;

                    if(uniqueCells[i].discovered == 0)
                    {
                        newlyDiscoveredCellCount++;

                        WorleyCell cell = uniqueCells[i];
                        cell.discovered = 1;
                        uniqueCells[i] = cell;
                    }
                }
            }

            if(newlyDiscoveredCellCount > 0)
            {
                DynamicBuffer<SquareToCreate> squaresToCreate = commandBuffer.SetBuffer<SquareToCreate>(jobIndex, mapSquareEntity);
                
                for(int d = 0; d < 8; d++)
                {
                    float3 adjacentPosition = position.Value + (directions[d] * squareWidth);
                    squaresToCreate.Add(new SquareToCreate { squarePosition = adjacentPosition });
                } 
            }

            if(discoveredCellCount == uniqueCells.Length)
                commandBuffer.SetComponent<Tags.CellDiscoveryComplete>(jobIndex, mapSquareEntity, new Tags.CellDiscoveryComplete());          

        }
        
        /*bool UniqueCellsContainsCell(DynamicBuffer<WorleyCell> uniqueCells, WorleyCell cell)
        {
            for(int i = 0; i < uniqueCells.Length; i++)
                if(uniqueCells[i].index.Equals(cell.index))
                    return true;

            return false;
        }

        sbyte MapSquareIsAtEge(DynamicBuffer<WorleyCell> uniqueCells, WorleyCell cell)
        {
            if(uniqueCells.Length == 1 && uniqueCells[0].value == cell.value)
                return 0;
            else
                return 1;
        } */
    }
}
