using UnityEngine;
using Unity.Entities;
using MyComponents;
using Unity.Rendering;
using UnityEditor;
using Unity.Transforms;

public class Bootstrapper
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void Initialise()
    {
        Entity playerEntity = CreatePlayer();
        MapSquareSystem.playerEntity = playerEntity;
        CameraSystem.playerEntity = playerEntity;
    }
 
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    public static void InitializeAfterSceneLoad()
    {
        //CreatePlayer();
    }

    static Entity CreatePlayer()
    {
        EntityManager entityManager = World.Active.GetOrCreateManager<EntityManager>();

        //  Archetype
        EntityArchetype playerArchetype = entityManager.CreateArchetype(
            ComponentType.Create<Tags.PlayerEntity>(),
            ComponentType.Create<Movement>(),
            ComponentType.Create<Stats>(),
            ComponentType.Create<Position>(),
            ComponentType.Create<MeshInstanceRendererComponent>()
        );

        //  Entity
        Entity playerEntity = entityManager.CreateEntity(playerArchetype);

        //  Mesh
        MeshInstanceRenderer renderer = new MeshInstanceRenderer();
        GameObject capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
		renderer.mesh = capsule.GetComponent<MeshFilter>().mesh;
        GameObject.Destroy(capsule);
		renderer.material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/ShaderGraphTest.mat");
		entityManager.AddSharedComponentData<MeshInstanceRenderer>(playerEntity, renderer);

        entityManager.SetComponentData<Position>(playerEntity, new Position());

        Stats stats = new Stats { speed = 20.0f };
        entityManager.SetComponentData<Stats>(playerEntity, stats);

        Movement movement = new Movement { size = 2 };
        entityManager.SetComponentData<Movement>(playerEntity, movement);

        return playerEntity;
    }
}