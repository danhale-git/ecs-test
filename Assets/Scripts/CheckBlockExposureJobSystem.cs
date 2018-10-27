using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

class CheckBlockExposureJobSystem
{
	struct CheckJob : IJobParallelFor
	{
		public NativeArray<Faces> exposedFaces;

		[ReadOnly] public NativeArray<int> right;
		[ReadOnly] public NativeArray<int> left;
		[ReadOnly] public NativeArray<int> up;
		[ReadOnly] public NativeArray<int> down;
		[ReadOnly] public NativeArray<int> forward;
		[ReadOnly] public NativeArray<int> back;


		[ReadOnly] public NativeArray<int> blocks;
		[ReadOnly] public int chunkSize;
		[ReadOnly] public JobUtil util;

		int FaceExposed(float3 position, float3 direction)
		{
			float3 pos = position + direction;

			if(pos.x == chunkSize) 	return right[util.WrapAndFlatten(pos, chunkSize)] 	== 0 ? 1 : 0;
			if(pos.x < 0)			return left[util.WrapAndFlatten(pos, chunkSize)] 	== 0 ? 1 : 0;
			if(pos.y == chunkSize) 	return up[util.WrapAndFlatten(pos, chunkSize)] 		== 0 ? 1 : 0;
			if(pos.y < 0)			return down[util.WrapAndFlatten(pos, chunkSize)]	== 0 ? 1 : 0;
			if(pos.z == chunkSize) 	return forward[util.WrapAndFlatten(pos, chunkSize)] == 0 ? 1 : 0;
			if(pos.z < 0)			return back[util.WrapAndFlatten(pos, chunkSize)] 	== 0 ? 1 : 0;

			return blocks[util.Flatten(pos, chunkSize)] == 0 ? 1 : 0;
		}

		public void Execute(int i)
		{
			//	Get local position in heightmap
			float3 pos = util.Unflatten(i, chunkSize);

			//	Air blocks can't be exposed
			//	TODO is this right? Maybe prevent drawing air block in mesh code instead
			if(blocks[i] == 0) return;

			int right, left, up, down, forward, back;

			//	TODO check adjacent chunks instead of this silly if statement
			right =	FaceExposed(pos, new float3( 1,	0, 0));
			left = 	FaceExposed(pos, new float3(-1,	0, 0));
			up =   	FaceExposed(pos, new float3( 0,	1, 0));
			down = 	FaceExposed(pos, new float3( 0,-1, 0));
			forward=FaceExposed(pos, new float3( 0,	0, 1));
			back = 	FaceExposed(pos, new float3( 0,	0,-1));

			exposedFaces[i] = new Faces(right, left, up, down, forward, back, 0);
		}
	}

	public Faces[] GetExposure(int batchSize, int[][] _adjacent, int[] _blocks, out int faceCount)
	{
		int chunkSize = MapManager.chunkSize;

		//	Native and normal array
		var blocks = new NativeArray<int>(_blocks.Length, Allocator.TempJob);
		blocks.CopyFrom(_blocks);

		var exposedFaces = new NativeArray<Faces>(_blocks.Length, Allocator.TempJob);
		Faces[] exposedFacesArray = new Faces[exposedFaces.Length];

		NativeArray<int>[] adjacent = new NativeArray<int>[] {
			new NativeArray<int>(_blocks.Length, Allocator.TempJob),
			new NativeArray<int>(_blocks.Length, Allocator.TempJob),
			new NativeArray<int>(_blocks.Length, Allocator.TempJob),
			new NativeArray<int>(_blocks.Length, Allocator.TempJob),
			new NativeArray<int>(_blocks.Length, Allocator.TempJob),
			new NativeArray<int>(_blocks.Length, Allocator.TempJob)
		};

		for(int i = 0; i < 6; i++)
			adjacent[i].CopyFrom(_adjacent[i]);

		var job = new CheckJob(){
			exposedFaces = exposedFaces,
			blocks = blocks,
			chunkSize = chunkSize,
			util = new JobUtil(),

			right = adjacent[0],
			left = adjacent[1],
			up = adjacent[2],
			down = adjacent[3],
			forward = adjacent[4],
			back = adjacent[5]
			};
		
		//  Fill native array
        JobHandle jobHandle = job.Schedule(_blocks.Length, batchSize);
        jobHandle.Complete();

		//	Copy to normal array and return
		exposedFaces.CopyTo(exposedFacesArray);

		blocks.Dispose();
		exposedFaces.Dispose();

		for(int i = 0; i < 6; i++)
			adjacent[i].Dispose();

		faceCount = GetExposedBlockIndices(exposedFacesArray);

		return exposedFacesArray;
	}

	int GetExposedBlockIndices(Faces[] faces)
	{
		int faceCount = 0;
		for(int i = 0; i < faces.Length; i++)
		{
			int count = faces[i].count;
			if(count > 0)
			{
				faces[i].faceIndex = faceCount;
				faceCount += count;
			}
		}
		return faceCount;
	}

	
}
