using UnityEngine;
using Unity.Entities;
using MyComponents;
using UnityEditor;
using Unity.Rendering;
using Unity.Transforms;

public class Bootstrapper
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void Initialise()
    {
        Entity playerEntity = CreatePlayer();
        //MapManagerSystem.playerEntity = playerEntity;//TODO: REMOVE
        MapSquareSystem.playerEntity = playerEntity;
        CameraSystem.playerEntity = playerEntity;
        PlayerInputSystem.playerEntity = playerEntity;
    }
 
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    public static void InitializeAfterSceneLoad()
    {
    }

    static Entity CreatePlayer()
    {
        EntityManager entityManager = World.Active.GetOrCreateManager<EntityManager>();

        //  Archetype
        EntityArchetype playerArchetype = entityManager.CreateArchetype(
            ComponentType.ReadWrite<Tags.PlayerEntity>(),
            ComponentType.ReadWrite<PhysicsEntity>(),
            ComponentType.ReadWrite<Stats>(),
            ComponentType.ReadWrite<Translation>(),
            ComponentType.ReadWrite<RenderMeshProxy>()
        );

        //  Entity
        Entity playerEntity = entityManager.CreateEntity(playerArchetype);

        //  Mesh
        RenderMesh renderer = new RenderMesh();
        GameObject capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
		renderer.mesh = capsule.GetComponent<MeshFilter>().mesh;
        GameObject.Destroy(capsule);
		renderer.material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/ShaderGraphTest.mat");
		entityManager.AddSharedComponentData<RenderMesh>(playerEntity, renderer);

        //  Position
        entityManager.SetComponentData<Translation>(playerEntity, new Translation());

        //  Stats
        Stats stats = new Stats { speed = 20.0f };
        entityManager.SetComponentData<Stats>(playerEntity, stats);

        //  Movement
        PhysicsEntity movement = new PhysicsEntity { size = 2 };
        entityManager.SetComponentData<PhysicsEntity>(playerEntity, movement);

        return playerEntity;
    }
}