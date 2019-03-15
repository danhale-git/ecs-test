/*using Unity.Jobs;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Transforms;
using MyComponents;

public struct DiscoverMapSquareJob : IJob
{
    public EntityCommandBuffer commandBuffer;
    public EntityArchetype mapSquareArchetype;
    public Entity mapSquareEntity;
    public DynamicBuffer<WorleyCell> uniqueCells;
    public sbyte worleyNoiseIsGenerated;

    public MapMatrix<Entity> mapMatrix;
    public NativeList<CellMapSquare> allMapSquares;
    public NativeQueue<float3> squareQueue;

    [ReadOnly] public WorleyCell currentCell;

    [ReadOnly] public float3 squarePosition;
    [ReadOnly] public NativeArray<float3> directions;
    [ReadOnly] public WorleyNoiseGenerator worleyNoise;
    [ReadOnly] public int squareWidth;
    [ReadOnly] public JobUtil util;


    public void Execute()
    {
        if(worleyNoiseIsGenerated == 0)
            uniqueCells = GenerateWorleyNoise();

        if(!UniqueCellsContainsCell(uniqueCells, currentCell)) return;

        mapMatrix.SetAsDiscovered(true, squarePosition);

        allMapSquares.Add(new CellMapSquare{
                entity = mapSquareEntity,
                edge = MapSquareIsAtEge(uniqueCells, currentCell)
            }
        );

        for(int d = 0; d < 8; d++)
        {
            float3 adjacentPosition = squarePosition + (directions[d] * squareWidth);
            if(!mapMatrix.SquareIsDiscovered(adjacentPosition))
            {
                squareQueue.Enqueue(adjacentPosition);
            }
        }
    }
    
    bool UniqueCellsContainsCell(DynamicBuffer<WorleyCell> uniqueCells, WorleyCell cell)
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
    }

    public DynamicBuffer<WorleyCell> GenerateWorleyNoise()
    {
        NativeArray<WorleyNoise> worleyNoiseMap = new NativeArray<WorleyNoise>((int)math.pow(squareWidth, 2), Allocator.TempJob);

        for(int i = 0; i < worleyNoiseMap.Length; i++)
        {
            float3 position = util.Unflatten2D(i, squareWidth) + squarePosition;
            worleyNoiseMap[i] = worleyNoise.GetEdgeData(position.x, position.z);
        }

        DynamicBuffer<WorleyNoise> worleyNoiseBuffer = commandBuffer.SetBuffer<WorleyNoise>(mapSquareEntity);
        worleyNoiseBuffer.CopyFrom(worleyNoiseMap);

        NativeArray<WorleyCell> worleyCellSet = UniqueWorleyCellSet(worleyNoiseMap);
        DynamicBuffer<WorleyCell> uniqueWorleyCells = commandBuffer.SetBuffer<WorleyCell>(mapSquareEntity);
        uniqueWorleyCells.CopyFrom(worleyCellSet);

        worleyNoiseMap.Dispose();
        worleyCellSet.Dispose();

        return uniqueWorleyCells;
    }

    public NativeArray<WorleyCell> UniqueWorleyCellSet(NativeArray<WorleyNoise> worleyNoiseMap)
    {
        NativeList<WorleyNoise> noiseSet = Util.Set<WorleyNoise>(worleyNoiseMap, Allocator.Temp);
        NativeArray<WorleyCell> cellSet = new NativeArray<WorleyCell>(noiseSet.Length, Allocator.TempJob);

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
 */