using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class ChunkSystem : ComponentSystem
{
	PlayerController player;

	EntityManager entityManager;
	EntityArchetype ChunkArchetype;
	public Dictionary<float3, Entity> chunkDictionary;

	public static int chunkSize = 8;
	public static int viewDistance = 10;

	protected override void OnCreateManager ()
	{
		player = GameObject.FindObjectOfType<PlayerController>();

		entityManager = World.Active.GetOrCreateManager<EntityManager> ();
		chunkDictionary = new Dictionary<float3, Entity> ();

		ChunkArchetype = entityManager.CreateArchetype
			(
			ComponentType.Create<Chunk> (),  // entity type
			ComponentType.Create<Block> (),  // buffer array of the chunks blocks
			ComponentType.Create<CREATE> ()
			);

		GenerateRadius(Vector3.zero, viewDistance);
	}


	//	Generate chunks on timer expire
	float timer = 0;
	protected override void OnUpdate()
	{
		if(Time.fixedTime - timer > 1)
		{
			timer = Time.fixedTime;
			GenerateRadius(
				Util.VoxelOwner(player.transform.position, chunkSize),
				viewDistance);
		}
	}

	//	Generate chunks
	public void GenerateRadius(Vector3 center, int radius)
	{
		center = new Vector3(center.x, 0, center.z);
		//	Create chunk instance
		PositionsInSpiral(center, radius+1, CreateNewChunk);
		//	Generate blocks 
		//PositionsInSpiral(center, radius+1, BlockStage);
		//	Create Mesh
		//PositionsInSpiral(center, radius, DrawStage);
	}

	private void CreateNewChunk (Vector3 position)
	{
		if (chunkDictionary.ContainsKey (position))
		{
			// load the chunk instead of creating a new one, tag it for inclusion into mesh
			return;
		}
		if (position.y < 0) return;

		Entity newChunkEntity = entityManager.CreateEntity (ChunkArchetype);
		Chunk newChunk = new Chunk { worldPosition = position };
		entityManager.SetComponentData (newChunkEntity, newChunk);

		chunkDictionary.Add (position, newChunkEntity);
//		Debug.Log("chunk created at "+position);
	}

	//	Execute delegate in a spiral of chunk positions moving out from center
	delegate void GenerationStage(Vector3 position);
	void PositionsInSpiral(Vector3 center, int radius, GenerationStage ExecuteStage)
	{
		Vector3 position = center;

		//	Generate center block
		ExecuteStage(position);

		radius-=1;//DEBUG

		int increment = 1;
		for(int i = 0; i < radius; i++)
		{
			//	right then back
			for(int r = 0; r < increment; r++)
			{
				position += Vector3.right * chunkSize;
				ExecuteStage(position);
			}
			for(int b = 0; b < increment; b++)
			{
				position += Vector3.back * chunkSize;
				ExecuteStage(position);
			}

			increment++;

			//	left then forward
			for(int l = 0; l < increment; l++)
			{
				position += Vector3.left * chunkSize;
				ExecuteStage(position);
			}
			for(int f = 0; f < increment; f++)
			{
				position += Vector3.forward * chunkSize;
				ExecuteStage(position);
			}

			increment++;
		}
		//	Square made by spiral is always missing one corner
		for(int r = 0; r < increment - 1; r++)
		{
			position += Vector3.right * chunkSize;
			ExecuteStage(position);
		}
	}
}