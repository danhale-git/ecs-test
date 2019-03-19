using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;

namespace MyComponents
{
	#region Player

	public struct Stats : IComponentData
	{
		public float speed;
	}

	public struct PhysicsEntity : IComponentData
	{
		public float3 positionChangePerSecond;
		public float3 size;
		public Entity currentMapSquare;
	}

	#endregion

	#region Map

	public enum SlopeType { NOTSLOPED, FLAT, INNERCORNER, OUTERCORNER }
	public enum SlopeFacing { NWSE, SWNE }

	public struct MapSquare : IComponentData
	{
		public float3 position;

		public int topBlock;
		public int bottomBlock;

		public int topDrawBounds;
		public int bottomDrawBounds;

		public int topBlockBuffer;
		public int bottomBlockBuffer;

		public int blockDrawArrayLength;
		public int blockDataArrayLength;

		public int drawIndexOffset;
	}

	[InternalBufferCapacity(0)]
	public struct Topology : IBufferElementData
	{
		public float noise;
		public int height;
		public TerrainTypes type;
	}

	[InternalBufferCapacity(0)]
	public struct WorleyNoise : IBufferElementData, System.IComparable<WorleyNoise>
	{
		public int CompareTo(WorleyNoise other)
		{
			return currentCellValue.CompareTo(other.currentCellValue);
		}

		public float3 currentCellPosition;
		public int2 currentCellIndex;
		public float currentCellValue, distance2Edge, adjacentCellValue;
	}

	[InternalBufferCapacity(0)]
	public struct WorleyCell : IBufferElementData, System.IComparable<WorleyCell>
	{
		public int CompareTo(WorleyCell other)
		{
			return value.CompareTo(other.value);
		}

		public float value;
		public int2 index;
		public float3 position;

		public sbyte discovered;
	}

	public struct AdjacentSquares : IComponentData
	{
		public Entity right;
		public Entity left;
		public Entity front;
		public Entity back;
		public Entity frontRight;
		public Entity frontLeft;
		public Entity backRight;
		public Entity backLeft;

		public Entity this[int side]
		{
			get
			{
				switch (side)
				{
					case 0: return right;
					case 1: return left;
					case 2: return front;
					case 3: return back;
					case 4: return frontRight;
					case 5: return frontLeft;
					case 6: return backRight;
					case 7: return backLeft;

					default: throw new System.ArgumentOutOfRangeException("Index out of range 7: " + side);
				}
			}
		}

		public Entity GetByDirection(float3 dir)
		{
			if	   (dir.x ==  1 && dir.y == 0 && dir.z ==  0) return right;
			else if(dir.x == -1 && dir.y == 0 && dir.z ==  0) return left;
			else if(dir.x ==  0 && dir.y == 0 && dir.z ==  1) return front;
			else if(dir.x ==  0 && dir.y == 0 && dir.z == -1) return back;
			else if(dir.x ==  1 && dir.y == 0 && dir.z ==  1) return frontRight;
			else if(dir.x == -1 && dir.y == 0 && dir.z ==  1) return frontLeft;
			else if(dir.x ==  1 && dir.y == 0 && dir.z == -1) return backRight;
			else if(dir.x == -1 && dir.y == 0 && dir.z == -1) return backLeft;
			else throw new System.ArgumentOutOfRangeException("Index out of range 7: " + dir);
		}

		public NativeArray<int> GetLowestBlocks(Allocator label)
		{
			EntityManager entityManager = World.Active.GetOrCreateManager<EntityManager>();

			NativeArray<int> lowestBlocks = new NativeArray<int>(8, label);
			for(int i = 0; i < 8; i++)
			{
				MapSquare adjacentSquare = entityManager.GetComponentData<MapSquare>(this[i]);
				lowestBlocks[i] = adjacentSquare.bottomBlockBuffer;
			}
			return lowestBlocks;
		}
	}

	[InternalBufferCapacity(0)]
	public struct Block : IBufferElementData
	{
		public int debug;
		
		public int type;
		public float3 localPosition;

		public byte isSloped;
		public BlockSlope slope;
	}

	public struct BlockSlope
	{
		public sbyte frontRightSlope;
		public sbyte backRightSlope;
		public sbyte frontLeftSlope;
		public sbyte backLeftSlope;

		public SlopeType slopeType;
		public SlopeFacing slopeFacing;

		public float2 GetSlopeVerts(int side)
		{
			switch(side)
			{
				case 0: return new float2(frontRightSlope, backRightSlope);	//	Right
				case 1:	return new float2(frontLeftSlope, backLeftSlope);	//	Left
				case 2: return new float2(frontRightSlope, frontLeftSlope);	//	Front
				case 3: return new float2(backRightSlope, backLeftSlope);	//	Back
				default: throw new System.ArgumentOutOfRangeException("Index out of range 3: " + side);
			}
		}
	}

	[InternalBufferCapacity(0)]
	public struct VertBuffer : IBufferElementData
	{
		public float3 vertex;
	}
	[InternalBufferCapacity(0)]
	public struct NormBuffer : IBufferElementData
	{
		public float3 normal;
	}
	[InternalBufferCapacity(0)]
	public struct TriBuffer : IBufferElementData
	{
		public int triangle;
	}
	[InternalBufferCapacity(0)]
	public struct ColorBuffer : IBufferElementData
	{
		public float4 color;
	}
	[InternalBufferCapacity(0)]
	public struct Faces : IBufferElementData
	{
		public enum Exp { HIDDEN, FULL, HALFOUT, HALFIN }

		public int right, left, front, back, up, down;
		public int count;
		public int faceIndex, triIndex, vertIndex;
		
		public void SetCount()
		{
			if(right	> 0) count++;
			if(left		> 0) count++;
			if(front	> 0) count++;
			if(back		> 0) count++;
			if(up		> 0) count++;
			if(down		> 0) count++;
		}

		public int this[int side]
		{
			get
			{
				switch(side)
				{
					case 0: return right;
					case 1: return left;
					case 2: return front;
					case 3: return back;
					case 4: return up;
					case 5: return down;
					default: throw new System.ArgumentOutOfRangeException("Index out of range 7: " + side);
				}
			}

			set
			{
				switch(side)
				{
					case 0: right = value; break;
					case 1: left = value; break;
					case 2: front = value; break;
					case 3: back = value; break;
					case 4: up = value; break;
					case 5: down = value; break;
					default: throw new System.ArgumentOutOfRangeException("Index out of range 7: " + side);
				}
			}
		}
	}

	[InternalBufferCapacity(100)]
	public struct PendingChange : IBufferElementData
	{
		public Block block;
	}
	[InternalBufferCapacity(100)]
	public struct LoadedChange : IBufferElementData
	{
		public Block block;
	}
	[InternalBufferCapacity(100)]
	public struct UnsavedChange : IBufferElementData
	{
		public Block block;
	}

	

	#endregion
}

#region Tags

namespace Tags
{
	public struct AllCellsDiscovered : IComponentData { }
	public struct GenerateWorleyNoise : IComponentData { }
	public struct CreateAdjacentSquares : IComponentData { }

	public struct SetHorizontalDrawBuffer : IComponentData { }
	public struct GenerateTerrain : IComponentData { }
	public struct GetAdjacentSquares : IComponentData { }
	public struct LoadChanges : IComponentData { }
	public struct SetDrawBuffer : IComponentData { }
	public struct SetBlockBuffer : IComponentData { }
	public struct GenerateBlocks : IComponentData { }
	public struct SetSlopes : IComponentData { }
	public struct DrawMesh : IComponentData { }
	
	public struct Redraw : IComponentData { }
	public struct BufferChanged : IComponentData { }

	public struct RemoveMapSquare : IComponentData { }

	public struct InnerBuffer : IComponentData { }
	public struct OuterBuffer : IComponentData { }	
	public struct EdgeBuffer : IComponentData { }

	public struct PlayerEntity : IComponentData { }
}

#endregion

namespace UpdateGroups
{
	//[UpdateAfter(typeof(MapHorizontalDrawBufferSystem))]
	[UpdateAfter(typeof(MapHorizontalDrawAreaSystem))]
	public class NewMapSquareUpdateGroup : ComponentSystemGroup { }
}