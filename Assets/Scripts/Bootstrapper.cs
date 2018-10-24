using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Unity.Entities;
using Unity.Transforms;
using Unity.Rendering;
using Unity.Mathematics;

public class Bootstrapper
{
    static EntityManager entityManager;
    static MeshInstanceRenderer renderer;
    static EntityArchetype archetype;

    public static ChunkManager chunks;
    public static MapManager mapManager;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Initialise()
    {
        chunks = new ChunkManager();
        mapManager = new MapManager();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Start()
    {
        int chunkSize = ChunkManager.chunkSize;
        for(int x = 0; x < 1; x++)
            for(int z = 0; z < 1; z++)
            {
                Vector3 position = new Vector3(x*chunkSize, 0, z*chunkSize);
                int[] heightMap = mapManager.GenerateMapSquare(position);
                chunks.GenerateChunk(position, heightMap);
            }

    }

}
