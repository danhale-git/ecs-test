using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

class CheckBlockExposureJobSystem
{
	struct CheckJob : IJobParallelFor
	{
		public NativeArray<Faces> exposedFaces;

		[ReadOnly] public NativeArray<int> blocks;
		[ReadOnly] public int chunkSize;
		[ReadOnly] public JobUtil util;

		public void Execute(int i)
		{
			//	Get local position in heightmap
			float3 pos = util.Unflatten(i, chunkSize);

			//	Air blocks can't be exposed
			//	TODO is this right? Maybe prevent drawing air block in mesh code instead
			if(blocks[i] == 0) return;

			int right, left, up, down, forward, back;

			//	TODO check adjacent chunks instead of this silly if statement
			if(	  !(pos.x == chunkSize-1 	|| pos.y == chunkSize-1 || pos.z == chunkSize-1 ||
					pos.x == 0 				|| pos.y == 0 			|| pos.z == 0)	)
			{
				right =	blocks[util.Flatten(pos.x+1,	pos.y,		pos.z, chunkSize)] 		== 0 ? 1 : 0;
				left = 	blocks[util.Flatten(pos.x-1,	pos.y,		pos.z, chunkSize)] 		== 0 ? 1 : 0;
				up =   	blocks[util.Flatten(pos.x,		pos.y+1,	pos.z, chunkSize)] 		== 0 ? 1 : 0;
				down = 	blocks[util.Flatten(pos.x,		pos.y-1,	pos.z, chunkSize)] 		== 0 ? 1 : 0;
				forward=blocks[util.Flatten(pos.x,		pos.y,		pos.z+1, chunkSize)]	== 0 ? 1 : 0;
				back = 	blocks[util.Flatten(pos.x,		pos.y,		pos.z-1, chunkSize)] 	== 0 ? 1 : 0;

				exposedFaces[i] = new Faces(right, left, up, down, forward, back, 0);
			}
			else
			{
				exposedFaces[i] = new Faces(0,0,0,0,0,0,0);
			}
		}
	}

	public Faces[] GetExposure(int[] _blocks, out int faceCount)
	{
		int chunkSize = ChunkManager.chunkSize;

		//	Native and normal array
		var blocks = new NativeArray<int>(_blocks.Length, Allocator.TempJob);
		blocks.CopyFrom(_blocks);

		var exposedFaces = new NativeArray<Faces>(_blocks.Length, Allocator.TempJob);
		Faces[] exposedFacesArray = new Faces[exposedFaces.Length];

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
		exposedFaces.CopyTo(exposedFacesArray);

		blocks.Dispose();
		exposedFaces.Dispose();

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
