using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Burst;

class CheckBlockExposureJobSystem
{
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

		var job = new CheckBlockExposureJob(){
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
