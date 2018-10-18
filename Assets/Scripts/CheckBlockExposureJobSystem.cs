using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

class CheckBlockExposureJobSystem
{
	struct CheckJob : IJobParallelFor
	{
		public NativeArray<int> exposedSides;

		[ReadOnly]public NativeArray<int> blocks;
		[ReadOnly] public int chunkSize;
		[ReadOnly] public JobUtil util;

		public void Execute(int i)
		{
			//	Get local position in heightmap
			float3 pos = util.Unflatten(i, chunkSize);

			//	TODO check adjacent chunks instead
			if(	  !(pos.x == chunkSize-1 	|| pos.y == chunkSize-1 || pos.z == chunkSize-1 ||
					pos.x == 0 				|| pos.y == 0 			|| pos.z == 0)	)
			{
				/*int index = i * 6;

				exposedSides[i+0] = blocks[util.Flatten((int)pos.x,		(int)pos.y+1,	(int)pos.z)] 	== 0 ? 1 : 0;
				exposedSides[i+1] = blocks[util.Flatten((int)pos.x,		(int)pos.y-1,	(int)pos.z)] 	== 0 ? 1 : 0;
				exposedSides[i+2] = blocks[util.Flatten((int)pos.x+1,	(int)pos.y,		(int)pos.z)] 	== 0 ? 1 : 0;
				exposedSides[i+3] = blocks[util.Flatten((int)pos.x-1,	(int)pos.y,		(int)pos.z)] 	== 0 ? 1 : 0;
				exposedSides[i+4] = blocks[util.Flatten((int)pos.x,		(int)pos.y,		(int)pos.z+1)]	== 0 ? 1 : 0;
				exposedSides[i+5] = blocks[util.Flatten((int)pos.x,		(int)pos.y,		(int)pos.z-1)] 	== 0 ? 1 : 0;*/
			}
			
			//	TODO find a way to avoid indexes larger than _blocks.Length - nested native arrays? nested job execution?
			//	Maybe fuck the array and just start generated vertices and triangles. 

		}
	}

	public int[] GetExposure(int[] _blocks)
	{
		int chunkSize = ChunkManager.chunkSize;

		//	Native and normal array
		var blocks = new NativeArray<int>(_blocks.Length, Allocator.TempJob);
		blocks.CopyFrom(_blocks);

		var exposedSides = new NativeArray<int>(_blocks.Length * 6, Allocator.TempJob);
		int[] exposedSidesArray = new int[exposedSides.Length];

		//var vertices = new NativeList<float3>();
		//var triangles = new NativeList<int>();

		var job = new CheckJob(){
			exposedSides = exposedSides,
			blocks = blocks,
			chunkSize = chunkSize,
			util = new JobUtil()
			};
		
		//  Fill native array
        JobHandle jobHandle = job.Schedule(_blocks.Length, 64);
        jobHandle.Complete();

		//	Copy to normal array and return
		exposedSides.CopyTo(exposedSidesArray);

		blocks.Dispose();
		exposedSides.Dispose();

		return exposedSidesArray;
	}
}
