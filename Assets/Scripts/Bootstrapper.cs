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

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Initialise()
    {
        chunks = new ChunkManager();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Start()
    {
       chunks.GenerateChunk();
    }

}
