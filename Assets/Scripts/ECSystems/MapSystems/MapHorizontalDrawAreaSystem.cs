using Unity.Jobs;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Transforms;
using MyComponents;

[UpdateAfter(typeof(MapHorizontalDrawAreaSystem))]
public class HorizontalDrawAreaBarrier : BarrierSystem { }

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
        return inputDependencies;
        
        SubMatrix viewSubMatrix = ViewSubMatrix();
        SubMatrix subMatrix = FitView(squareSystem.mapMatrix, viewSubMatrix);

        SetNewSquaresJob newSquaresJob = new SetNewSquaresJob{
            commandBuffer = drawAreaBarrier.CreateCommandBuffer().ToConcurrent(),
            viewSubMatrix = viewSubMatrix,
            drawBufferUtil = new DrawBufferUtil(squareWidth)
        };

        CheckInnerSquaresJob innerSquaresJob = new CheckInnerSquaresJob{
            commandBuffer = drawAreaBarrier.CreateCommandBuffer().ToConcurrent(),
            viewSubMatrix = viewSubMatrix,
            drawBufferUtil = new DrawBufferUtil(squareWidth)
        };

        CheckOuterSquaresJob outerSquaresJob = new CheckOuterSquaresJob{
            commandBuffer = drawAreaBarrier.CreateCommandBuffer().ToConcurrent(),
            viewSubMatrix = viewSubMatrix,
            drawBufferUtil = new DrawBufferUtil(squareWidth)
        };

        CheckEdgeSquaresJob edgeSquaresJob = new CheckEdgeSquaresJob{
            commandBuffer = drawAreaBarrier.CreateCommandBuffer().ToConcurrent(),
            viewSubMatrix = viewSubMatrix,
            drawBufferUtil = new DrawBufferUtil(squareWidth)
        };

        NativeArray<JobHandle> jobHandles = new NativeArray<JobHandle>(4, Allocator.Temp);
        jobHandles[0] = newSquaresJob.Schedule(this, inputDependencies);
        jobHandles[1] = innerSquaresJob.Schedule(this, inputDependencies);
        jobHandles[2] = outerSquaresJob.Schedule(this, inputDependencies);
        jobHandles[3] = edgeSquaresJob.Schedule(this, inputDependencies);

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

    [RequireSubtractiveComponent(typeof(Tags.EdgeBuffer), typeof(Tags.OuterBuffer), typeof(Tags.InnerBuffer))]
    public struct SetNewSquaresJob : IJobProcessComponentDataWithEntity<MapSquare, Position>
    {
        public EntityCommandBuffer.Concurrent commandBuffer;

        [ReadOnly] public SubMatrix viewSubMatrix;
        [ReadOnly] public DrawBufferUtil drawBufferUtil;

        public void Execute(Entity entity, int jobIndex, ref MapSquare mapSquare, ref Position position)
        {
            bool inRadius = !drawBufferUtil.IsOutsideSubMatrix(viewSubMatrix, position.Value);

            DrawBufferType buffer = drawBufferUtil.GetDrawBuffer(viewSubMatrix, position.Value);

            SetDrawBuffer(entity, buffer, commandBuffer, jobIndex);
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

    [RequireComponentTag(typeof(Tags.InnerBuffer))]
    public struct CheckInnerSquaresJob: IJobProcessComponentDataWithEntity<MapSquare, Position>
    {
        public EntityCommandBuffer.Concurrent commandBuffer;

        [ReadOnly] public SubMatrix viewSubMatrix;
        [ReadOnly] public DrawBufferUtil drawBufferUtil;

        public void Execute(Entity entity, int jobIndex, ref MapSquare mapSquare, ref Position position)
        {
            bool inRadius = !drawBufferUtil.IsOutsideSubMatrix(viewSubMatrix, position.Value);

            DrawBufferType buffer = drawBufferUtil.GetDrawBuffer(viewSubMatrix, position.Value);

            if(buffer == DrawBufferType.INNER) return;
            commandBuffer.RemoveComponent<Tags.InnerBuffer>(jobIndex, entity);

            if(buffer == DrawBufferType.NONE) return;
            else drawBufferUtil.SetDrawBuffer(entity, buffer, commandBuffer, jobIndex);

            //if(!inRadius)
            //    drawBufferUtil.RedrawMapSquare(entity, commandBuffer);
        }
    }

    [RequireComponentTag(typeof(Tags.OuterBuffer))]
    public struct CheckOuterSquaresJob: IJobProcessComponentDataWithEntity<MapSquare, Position>
    {
        public EntityCommandBuffer.Concurrent commandBuffer;

        [ReadOnly] public SubMatrix viewSubMatrix;
        [ReadOnly] public DrawBufferUtil drawBufferUtil;

        public void Execute(Entity entity, int jobIndex, ref MapSquare mapSquare, ref Position position)
        {
            bool inRadius = !drawBufferUtil.IsOutsideSubMatrix(viewSubMatrix, position.Value);

            DrawBufferType buffer = drawBufferUtil.GetDrawBuffer(viewSubMatrix, position.Value);

            if(buffer == DrawBufferType.OUTER) return;
            commandBuffer.RemoveComponent<Tags.OuterBuffer>(jobIndex, entity);

            if(buffer == DrawBufferType.NONE) return;
            else drawBufferUtil.SetDrawBuffer(entity, buffer, commandBuffer, jobIndex);

            //if(!inRadius)
            //    drawBufferUtil.RedrawMapSquare(entity, commandBuffer);
        }
    }

    [RequireComponentTag(typeof(Tags.EdgeBuffer))]
    public struct CheckEdgeSquaresJob: IJobProcessComponentDataWithEntity<MapSquare, Position>
    {
        public EntityCommandBuffer.Concurrent commandBuffer;

        [ReadOnly] public SubMatrix viewSubMatrix;
        [ReadOnly] public DrawBufferUtil drawBufferUtil;

        public void Execute(Entity entity, int jobIndex, ref MapSquare mapSquare, ref Position position)
        {
            bool inRadius = !drawBufferUtil.IsOutsideSubMatrix(viewSubMatrix, position.Value);

            DrawBufferType buffer = drawBufferUtil.GetDrawBuffer(viewSubMatrix, position.Value);

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
            commandBuffer.AddComponent<Tags.EdgeBuffer>(jobIndex, entity, new Tags.EdgeBuffer());

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

            CustomDebugTools.HorizontalBufferDebug(entity, (int)buffer);
        }

        /*void RedrawMapSquare(Entity entity, EntityCommandBuffer.Concurrent commandBuffer)
        {
            entityUtil.TryRemoveSharedComponent<RenderMesh>(entity, commandBuffer);
            entityUtil.TryAddComponent<Tags.DrawMesh>(entity, commandBuffer);
        } */
    }
    
    SubMatrix FitView(Matrix<Entity> matrix, SubMatrix subMatrix)
	{
        float3 subMatrixEdge = matrix.WorldToMatrixPosition(subMatrix.rootPosition) + subMatrix.width;

        if(subMatrixEdge.x >= matrix.width) subMatrixEdge.x = matrix.width - 1;
        if(subMatrixEdge.z >= matrix.width) subMatrixEdge.z = matrix.width - 1;

        //	Copy original matix to cache so it defaults to original matrix values
		NativeArray<int> cacheMatrix = new NativeArray<int>(matrix.Length, Allocator.Temp);
        for(int i = 0; i < cacheMatrix.Length; i++)
            cacheMatrix[i] = matrix.ItemIsSet(i) ? 1 : 0;

		//	Resulting matrix origin and dimensions
		int resultX = 0;
		int resultZ = 0;
		int resultSize = 0;
		
        float3 squareRootPosition = float3.zero;

		for(int x = (int)subMatrixEdge.x; x >= 0; x--)
			for(int z = (int)subMatrixEdge.z; z >= 0; z--)
			{
                int index = matrix.PositionToIndex(new float3(x, 0, z));

				//	At edge, matrix.width-1square size is 1 so default to original matrix
				if(x == matrix.width-1 || z == matrix.width-1) continue;

                int forwardIndex = matrix.PositionToIndex(new float3(x, 0,z+1));
                int rightIndex = matrix.PositionToIndex(new float3(x+1, 0,z));
                int diagonalIndex = matrix.PositionToIndex(new float3(x+1, 0,z+1));

				//	Square is 1, value is equal to 1 + lowed of the three adjacent squares
				if(matrix.ItemIsSet(index) &&
                    entityManager.HasComponent<Tags.AllCellsDiscovered>(matrix.GetItem(index)))
                {
                    cacheMatrix[index] = 1 + math.min(cacheMatrix[forwardIndex],
                                                math.min(   cacheMatrix[rightIndex],
                                                                cacheMatrix[diagonalIndex]));
                }

				//	Largest square so far, store values
				if(cacheMatrix[index] > resultSize)
				{
					resultX = x;
					resultZ = z;
					resultSize = cacheMatrix[index];
				}

                float3 matrixPostiion = new float3(resultX, 0, resultZ);

                squareRootPosition = (matrixPostiion * squareWidth) + matrix.rootPosition;

                if(squareRootPosition.x < subMatrix.rootPosition.x || squareRootPosition.z < subMatrix.rootPosition.z)
                    break;
			}

        return new SubMatrix(squareRootPosition, resultSize);
	}
}