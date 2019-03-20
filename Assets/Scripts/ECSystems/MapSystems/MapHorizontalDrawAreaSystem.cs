using Unity.Jobs;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Transforms;
using MyComponents;

[UpdateAfter(typeof(MapHorizontalDrawAreaSystem))]
public class HorizontalDrawAreaBarrier : EntityCommandBufferSystem { }

[UpdateAfter(typeof(MapCellDiscoverySystem))]
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
        SubMatrix playerViewSubMatrix = ViewSubMatrix();
        SubMatrix subMatrix = LargestDiscoveredGridAroundPlayer(squareSystem.mapMatrix, playerViewSubMatrix);

        JobHandle newSquaresJob = new SetNewSquaresJob{
            commandBuffer = drawAreaBarrier.CreateCommandBuffer().ToConcurrent(),
            subMatrix = subMatrix,
            drawBufferUtil = new DrawBufferUtil(squareWidth)
        }.Schedule(this, inputDependencies);

        JobHandle innerSquaresJob = new CheckInnerSquaresJob{
            commandBuffer = drawAreaBarrier.CreateCommandBuffer().ToConcurrent(),
            subMatrix = subMatrix,
            drawBufferUtil = new DrawBufferUtil(squareWidth)
        }.Schedule(this, newSquaresJob);

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

        NativeArray<JobHandle> jobHandles = new NativeArray<JobHandle>(4, Allocator.Temp);
        jobHandles[0] = newSquaresJob;
        jobHandles[1] = innerSquaresJob;
        jobHandles[2] = outerSquaresJob;
        jobHandles[3] = edgeSquaresJob;

        JobHandle dependencies = JobHandle.CombineDependencies(jobHandles);
        jobHandles.Dispose();

        drawAreaBarrier.AddJobHandleForProducer(dependencies);

        return dependencies;
    }
    
    SubMatrix ViewSubMatrix()
    {
        float3 center = MapSquareSystem.currentMapSquare;
        int view = TerrainSettings.viewDistance;

        float3 veiwSubMatrixRoot = new float3(center.x - (view * squareWidth), 0, center.z - (view * squareWidth));
        int veiwSubMatrixWidth = (view * 2)+ 1;
        return new SubMatrix(veiwSubMatrixRoot, veiwSubMatrixWidth);
    }

    [RequireComponentTag(typeof(Tags.SetHorizontalDrawBuffer))]
    public struct SetNewSquaresJob : IJobProcessComponentDataWithEntity<MapSquare, Translation>
    {
        public EntityCommandBuffer.Concurrent commandBuffer;

        [ReadOnly] public SubMatrix subMatrix;
        [ReadOnly] public DrawBufferUtil drawBufferUtil;

        public void Execute(Entity entity, int jobIndex, ref MapSquare mapSquare, ref Translation position)
        {
            DebugTools.IncrementDebugCount("buffer");

            bool inRadius = !drawBufferUtil.IsOutsideSubMatrix(subMatrix, position.Value);

            DrawBufferType buffer = drawBufferUtil.GetDrawBuffer(subMatrix, position.Value);

            drawBufferUtil.SetDrawBuffer(entity, buffer, commandBuffer, jobIndex);

            commandBuffer.RemoveComponent<Tags.SetHorizontalDrawBuffer>(jobIndex, entity);
        }
    }

    [RequireComponentTag(typeof(Tags.InnerBuffer))]
    public struct CheckInnerSquaresJob: IJobProcessComponentDataWithEntity<MapSquare, Translation>
    {
        public EntityCommandBuffer.Concurrent commandBuffer;

        [ReadOnly] public SubMatrix subMatrix;
        [ReadOnly] public DrawBufferUtil drawBufferUtil;

        public void Execute(Entity entity, int jobIndex, ref MapSquare mapSquare, ref Translation position)
        {
            DebugTools.IncrementDebugCount("buffer");

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
            DebugTools.IncrementDebugCount("buffer");

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
            DebugTools.IncrementDebugCount("buffer");

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
    
    SubMatrix LargestDiscoveredGridAroundPlayer(Matrix<Entity> matrix, SubMatrix clampMatrix)
	{
        float3 clampTopRight = matrix.WorldToMatrixPosition(clampMatrix.rootPosition) + clampMatrix.width;

        if(clampTopRight.x >= matrix.width) clampTopRight.x = matrix.width - 1;
        if(clampTopRight.z >= matrix.width) clampTopRight.z = matrix.width - 1;

        //	Copy original matix to cache so it defaults to original matrix values
		NativeArray<int> cacheMatrix = new NativeArray<int>(matrix.Length, Allocator.Temp);
        for(int i = 0; i < cacheMatrix.Length; i++)
            cacheMatrix[i] = SquareIsEligible(matrix, i) ? 1 : 0;

		int finalGridWidth = 0;
        float3 finalGridRoot = float3.zero;

		for(int x = (int)clampTopRight.x; x >= 0; x--)
			for(int z = (int)clampTopRight.z; z >= 0; z--)
			{
				if(TopOrRightEdge(matrix.width, x, z))
                    continue;

                float3 matrixPosition = new float3(x, 0, z);
                int index = matrix.PositionToIndex(matrixPosition);

				if(cacheMatrix[index] > 0)
                    cacheMatrix[index] = 1 + LowestAdjacentValue(matrix, cacheMatrix, x, z);

				if(cacheMatrix[index] > finalGridWidth)
				{
					finalGridWidth = cacheMatrix[index];
                    finalGridRoot = matrix.MatrixToWorldPosition(matrixPosition);

                    if( finalGridRoot.x < clampMatrix.rootPosition.x ||
                        finalGridRoot.z < clampMatrix.rootPosition.z)
                        break;
				}
			}

        return new SubMatrix(finalGridRoot, finalGridWidth);
	}

    bool SquareIsEligible(Matrix<Entity> matrix, int index)
    {
        return (matrix.ItemIsSet(index) &&
                entityManager.HasComponent<Tags.AllCellsDiscovered>(matrix.GetItem(index)));
    }

    bool TopOrRightEdge(int width, int x, int z)
    {
        return (x == width-1 || z == width-1);
    }

    int LowestAdjacentValue(Matrix<Entity> matrix, NativeArray<int> cacheMatrix, int x, int z)
    {
        int forwardIndex    = matrix.PositionToIndex(new float3(x,   0, z+1));
        int rightIndex      = matrix.PositionToIndex(new float3(x+1, 0, z  ));
        int diagonalIndex   = matrix.PositionToIndex(new float3(x+1, 0, z+1));

        return  math.min(cacheMatrix[forwardIndex],
                    math.min(cacheMatrix[rightIndex],
                                cacheMatrix[diagonalIndex]));
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