using UnityEngine;
using Unity.Entities;
using MyComponents;
using Unity.Rendering;
using UnityEditor;
using Unity.Transforms;

public class Bootstrapper
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    void Initialise()
    {
        
    }
 
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    public static void InitializeAfterSceneLoad()
    {
        EntityManager entityManager = World.Active.GetOrCreateManager<EntityManager>();

        //  Archetype
        EntityArchetype playerArchetype = entityManager.CreateArchetype(
            ComponentType.Create<Tags.PlayerEntity>(),
            ComponentType.Create<PlayerInput>(),
            ComponentType.Create<Position>(),
            ComponentType.Create<MeshInstanceRendererComponent>()
        );

        //  Entity
        Entity playerEntity = entityManager.CreateEntity(playerArchetype);

        //  Mesh
        MeshInstanceRenderer renderer = new MeshInstanceRenderer();
		renderer.mesh =  GameObject.CreatePrimitive(PrimitiveType.Capsule).GetComponent<MeshFilter>().mesh;
        Debug.Log(renderer.mesh.vertexCount);
		renderer.material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/ShaderGraphTest.mat");
		entityManager.AddSharedComponentData<MeshInstanceRenderer>(playerEntity, renderer);

        Position position = new Position { Value = GameObject.FindObjectOfType<PlayerController>().transform.position };
        entityManager.SetComponentData<Position>(playerEntity, position);
    }
}