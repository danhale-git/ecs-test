using Unity.Jobs;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Transforms;
using MyComponents;

[UpdateAfter(typeof(MapHorizontalDrawAreaSystem))]
public class HorizontalDrawAreaBarrier : EntityCommandBufferSystem { }

public class MapHorizontalDrawAreaSystem : JobComponentSystem
{
	public enum DrawBufferType { NONE, INNER, OUTER, EDGE }

    EntityManager entityManager;
    MapSquareSystem squareSystem;
    HorizontalDrawAreaBarrier drawAreaBarrier;

    int squareWidth;

    public struct SubMatrix
    {
        public readonly float3 rootPosition;
        public readonly int width;
        public int Length { get { return (int)math.pow(width, 2);} }
        public SubMatrix(float3 rootPosition, int width)
        {
            this.rootPosition = rootPosition;
            this.width = width;
        }
    }

    protected override void OnCreateManager()
    {
        entityManager = World.Active.GetOrCreateManager<EntityManager>();
        squareSystem = World.Active.GetOrCreateManager<MapSquareSystem>();
        drawAreaBarrier = World.Active.GetOrCreateManager<HorizontalDrawAreaBarrier>();

        squareWidth = TerrainSettings.mapSquareWidth;
    }
    
    protected override JobHandle OnUpdate(JobHandle inputDependencies)
    {
        SubMatrix subMatrix = LargestVisibleArea(squareSystem.mapMatrix, MapSquareSystem.currentMapSquare);

        JobHandle innerSquaresJob = new CheckInnerSquaresJob{
            commandBuffer = drawAreaBarrier.CreateCommandBuffer().ToConcurrent(),
            subMatrix = subMatrix,
            drawBufferUtil = new DrawBufferUtil(squareWidth)
        }.Schedule(this, inputDependencies);

        JobHandle outerSquaresJob = new CheckOuterSquaresJob{
            commandBuffer = drawAreaBarrier.CreateCommandBuffer().ToConcurrent(),
            subMatrix = subMatrix,
            drawBufferUtil = new DrawBufferUtil(squareWidth)
        }.Schedule(this, innerSquaresJob);

        JobHandle edgeSquaresJob = new CheckEdgeSquaresJob{
            commandBuffer = drawAreaBarrier.CreateCommandBuffer().ToConcurrent(),
            subMatrix = subMatrix,
            drawBufferUtil = new DrawBufferUtil(squareWidth)
        }.Schedule(this, outerSquaresJob);

        NativeArray<JobHandle> jobHandles = new NativeArray<JobHandle>(3, Allocator.Temp);
        jobHandles[0] = innerSquaresJob;
        jobHandles[1] = outerSquaresJob;
        jobHandles[2] = edgeSquaresJob;

        JobHandle dependencies = JobHandle.CombineDependencies(jobHandles);
        jobHandles.Dispose();

        drawAreaBarrier.AddJobHandleForProducer(dependencies);

        return dependencies;
    }

    SubMatrix LargestVisibleArea(Matrix<Entity> matrix, float3 worldPosition)
    {
        int eligibleRadius = LargestEligibleRadius(squareSystem.mapMatrix, worldPosition);

        float3 root = worldPosition - ((eligibleRadius) * squareWidth);
        int width = (eligibleRadius * 2) + 1;
        return new SubMatrix(root, width);
    }
    
    int LargestEligibleRadius(Matrix<Entity> matrix, float3 worldPosition)
    {
        for(int i = 1; i <= TerrainSettings.viewDistance; i++)
        {
            if(!AllSquaresInRadiusAreEligible(matrix, worldPosition, i))
                return i-1;
        }
        return TerrainSettings.viewDistance;
    }

    bool AllSquaresInRadiusAreEligible(Matrix<Entity> matrix, float3 centerWorld, int offset)
    {
        if(offset == 0) return false;
        float3 centerMatrix = matrix.WorldToMatrixPosition(centerWorld);
        if(!SquareIsEligible(matrix, centerMatrix))
            return false;

        for(int xz = -offset; xz <= offset; xz++)
        {
            float3 topPosition = new float3(xz, 0, offset) + centerMatrix;
            float3 bottomPosition = new float3(xz, 0, -offset) + centerMatrix;
            float3 rightPosition = new float3(offset, 0, xz) + centerMatrix;
            float3 leftPosition = new float3(-offset, 0, xz) + centerMatrix;

            if( !SquareIsEligible(matrix, topPosition) ||
                !SquareIsEligible(matrix, bottomPosition) ||
                !SquareIsEligible(matrix, rightPosition) ||
                !SquareIsEligible(matrix, leftPosition) )
                {
                    return false;
                }
        }
        return true;
    }

    bool SquareIsEligible(Matrix<Entity> matrix, float3 matrixPosition)
    {
        int index = matrix.PositionToIndex(matrixPosition);
        return (matrix.ItemIsSet(index) &&
                entityManager.HasComponent<Tags.AllCellsDiscovered>(matrix.GetItem(index)));
    }

    [RequireComponentTag(typeof(Tags.InnerBuffer))]
    public struct CheckInnerSquaresJob: IJobProcessComponentDataWithEntity<MapSquare, Translation>
    {
        public EntityCommandBuffer.Concurrent commandBuffer;

        [ReadOnly] public SubMatrix subMatrix;
        [ReadOnly] public DrawBufferUtil drawBufferUtil;

        public void Execute(Entity entity, int jobIndex, ref MapSquare mapSquare, ref Translation position)
        {
            bool inRadius = !drawBufferUtil.IsOutsideSubMatrix(subMatrix, position.Value);

            DrawBufferType buffer = drawBufferUtil.GetDrawBuffer(subMatrix, position.Value);

            if(buffer == DrawBufferType.INNER) return;
            commandBuffer.RemoveComponent<Tags.InnerBuffer>(jobIndex, entity);

            if(buffer == DrawBufferType.NONE) return;
            else drawBufferUtil.SetDrawBuffer(entity, buffer, commandBuffer, jobIndex);

            //if(!inRadius)
            //    drawBufferUtil.RedrawMapSquare(entity, commandBuffer);
        }
    }

    [RequireComponentTag(typeof(Tags.OuterBuffer))]
    public struct CheckOuterSquaresJob: IJobProcessComponentDataWithEntity<MapSquare, Translation>
    {
        public EntityCommandBuffer.Concurrent commandBuffer;

        [ReadOnly] public SubMatrix subMatrix;
        [ReadOnly] public DrawBufferUtil drawBufferUtil;

        public void Execute(Entity entity, int jobIndex, ref MapSquare mapSquare, ref Translation position)
        {
            bool inRadius = !drawBufferUtil.IsOutsideSubMatrix(subMatrix, position.Value);

            DrawBufferType buffer = drawBufferUtil.GetDrawBuffer(subMatrix, position.Value);

            if(buffer == DrawBufferType.OUTER) return;
            commandBuffer.RemoveComponent<Tags.OuterBuffer>(jobIndex, entity);

            if(buffer == DrawBufferType.NONE) return;
            else drawBufferUtil.SetDrawBuffer(entity, buffer, commandBuffer, jobIndex);

            //if(!inRadius)
            //    drawBufferUtil.RedrawMapSquare(entity, commandBuffer);
        }
    }

    [RequireComponentTag(typeof(Tags.EdgeBuffer))]
    public struct CheckEdgeSquaresJob: IJobProcessComponentDataWithEntity<MapSquare, Translation>
    {
        public EntityCommandBuffer.Concurrent commandBuffer;

        [ReadOnly] public SubMatrix subMatrix;
        [ReadOnly] public DrawBufferUtil drawBufferUtil;

        public void Execute(Entity entity, int jobIndex, ref MapSquare mapSquare, ref Translation position)
        {
            bool inRadius = !drawBufferUtil.IsOutsideSubMatrix(subMatrix, position.Value);

            DrawBufferType buffer = drawBufferUtil.GetDrawBuffer(subMatrix, position.Value);

            if(buffer == DrawBufferType.EDGE) return;
            commandBuffer.RemoveComponent<Tags.EdgeBuffer>(jobIndex, entity);

            if(buffer == DrawBufferType.NONE) return;
            else drawBufferUtil.SetDrawBuffer(entity, buffer, commandBuffer, jobIndex);

            //if(!inRadius)
            //    drawBufferUtil.RedrawMapSquare(entity, commandBuffer);
        }
    }

    public struct DrawBufferUtil
    {
        int squareWidth;
        public DrawBufferUtil(int squareWidth)
        {
            this.squareWidth = squareWidth;
        }

        public DrawBufferType GetDrawBuffer(SubMatrix square, float3 bufferWorldPosition)
        {
            if (IsOutsideSubMatrix(square, bufferWorldPosition)) return DrawBufferType.EDGE;
            else if (IsDistanceFromSubMatrixEdge(square, bufferWorldPosition, 0)) return DrawBufferType.EDGE;
            else if (IsDistanceFromSubMatrixEdge(square, bufferWorldPosition, 1)) return DrawBufferType.OUTER;
            else if (IsDistanceFromSubMatrixEdge(square, bufferWorldPosition, 2)) return DrawBufferType.INNER;
            else return DrawBufferType.NONE;
        }

        public bool IsDistanceFromSubMatrixEdge(SubMatrix square, float3 bufferWorldPosition, int distance = 0)
        {
            float3 localPosition = (bufferWorldPosition - square.rootPosition) / squareWidth;

            if( (localPosition.x == 0+distance || localPosition.x == (square.width-1)-distance) ||
                (localPosition.z == 0+distance || localPosition.z == (square.width-1)-distance) )
                return true;
            else
                return false;
        }

        public bool IsOutsideSubMatrix(SubMatrix square, float3 bufferWorldPosition)
        {
            float3 localPosition = (bufferWorldPosition - square.rootPosition) / squareWidth;

            if( localPosition.x < 0 || localPosition.x >= square.width ||
                localPosition.z < 0 || localPosition.z >= square.width )
                return true;
            else
                return false;
        }

        public void SetDrawBuffer(Entity entity, DrawBufferType buffer, EntityCommandBuffer.Concurrent commandBuffer, int jobIndex)
        {
            DebugTools.IncrementDebugCount("buffer");

            switch(buffer)
            {
                case DrawBufferType.INNER:
                    commandBuffer.AddComponent<Tags.InnerBuffer>(jobIndex, entity, new Tags.InnerBuffer());
                    break;
                case DrawBufferType.OUTER:
                    commandBuffer.AddComponent<Tags.OuterBuffer>(jobIndex, entity, new Tags.OuterBuffer());
                    break;
                case DrawBufferType.EDGE:
                    commandBuffer.AddComponent<Tags.EdgeBuffer>(jobIndex, entity, new Tags.EdgeBuffer());
                    break;
                default:
                    break;
            }
        }
    }
}