using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

class CheckBlockExposureJobSystem
{
	struct CheckJob : IJobParallelFor
	{
		public NativeArray<Faces> exposedFaces;

		[ReadOnly]public NativeArray<int> blocks;
		[ReadOnly] public int chunkSize;
		[ReadOnly] public JobUtil util;

		public void Execute(int i)
		{
			//	Get local position in heightmap
			float3 pos = util.Unflatten(i, chunkSize);

			int right, left, up, down, front, back;

			//	TODO check adjacent chunks instead of this silly if statement
			if(	  !(pos.x == chunkSize-1 	|| pos.y == chunkSize-1 || pos.z == chunkSize-1 ||
					pos.x == 0 				|| pos.y == 0 			|| pos.z == 0)	)
			{
				right =	blocks[util.Flatten(pos.x+1,	pos.y,		pos.z, chunkSize)] 		== 0 ? 1 : 0;
				left = 	blocks[util.Flatten(pos.x-1,	pos.y,		pos.z, chunkSize)] 		== 0 ? 1 : 0;
				up =   	blocks[util.Flatten(pos.x,		pos.y+1,	pos.z, chunkSize)] 		== 0 ? 1 : 0;
				down = 	blocks[util.Flatten(pos.x,		pos.y-1,	pos.z, chunkSize)] 		== 0 ? 1 : 0;
				front =	blocks[util.Flatten(pos.x,		pos.y,		pos.z+1, chunkSize)]	== 0 ? 1 : 0;
				back = 	blocks[util.Flatten(pos.x,		pos.y,		pos.z-1, chunkSize)] 	== 0 ? 1 : 0;

				exposedFaces[i] = new Faces(right, left, up, down, front, back);
			}
			else
			{
				exposedFaces[i] = new Faces(0,0,0,0,0,0);
			}
		}
	}

	public Faces[] GetExposure(int[] _blocks)
	{
		int chunkSize = ChunkManager.chunkSize;

		//	Native and normal array
		var blocks = new NativeArray<int>(_blocks.Length, Allocator.TempJob);
		blocks.CopyFrom(_blocks);

		var exposedFaces = new NativeArray<Faces>(_blocks.Length, Allocator.TempJob);
		Faces[] exposedSidesArray = new Faces[exposedFaces.Length];

		var job = new CheckJob(){
			exposedFaces = exposedFaces,
			blocks = blocks,
			chunkSize = chunkSize,
			util = new JobUtil()
			};
		
		//  Fill native array
        JobHandle jobHandle = job.Schedule(_blocks.Length, 1);
        jobHandle.Complete();

		//	Copy to normal array and return
		exposedFaces.CopyTo(exposedSidesArray);

		blocks.Dispose();
		exposedFaces.Dispose();

		return exposedSidesArray;
	}
}
