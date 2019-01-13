using System;
using UnityEngine;
using Unity.Jobs;
using Unity.Entities;
using Unity.Collections;
using Unity.Transforms;
using Unity.Mathematics;
using MyComponents;

[UpdateAfter(typeof(PhysicsSystem))]
public class CameraSystem : ComponentSystem
{
    EntityManager entityManager;
    int cubeSize;

    public static Entity playerEntity;

    Camera camera;
    float cameraSwivelSpeed = 1;
    float3 currentOffset = new float3(0, 10, 10);

    protected override void OnCreateManager()
    {
        entityManager = World.Active.GetOrCreateManager<EntityManager>();
        cubeSize = TerrainSettings.cubeSize;

        camera = GameObject.FindObjectOfType<Camera>();
    }

    protected override void OnUpdate()
    {
       MoveCamera();
    }

    void MoveCamera()
    {
        float3 playerPosition   = entityManager.GetComponentData<Position>(playerEntity).Value;

        float3 oldPosition      = camera.transform.position;
        Quaternion oldRotation  = camera.transform.rotation;

        //  Rotate around player y axis
        bool Q = Input.GetKey(KeyCode.Q);
        bool E = Input.GetKey(KeyCode.E);
        Quaternion cameraSwivel = Quaternion.identity;
        if( !(Q && E) )
        {
            if (Q) cameraSwivel     = Quaternion.Euler(new float3(0, -cameraSwivelSpeed, 0));
            else if(E) cameraSwivel = Quaternion.Euler(new float3(0, cameraSwivelSpeed, 0));
        }
        float3 rotateOffset = (float3)oldPosition - Util.RotateAroundCenter(cameraSwivel, oldPosition, playerPosition);

        //  Zoom with mouse wheel
        float3 zoomOffset = Input.GetAxis("Mouse ScrollWheel") * -currentOffset;

        //  Apply position changes
        currentOffset += rotateOffset;

        //  Clamp zoom 
        float3 withZoom = currentOffset + zoomOffset;
        float magnitude = math.sqrt(math.pow(withZoom.x, 2) + math.pow(withZoom.y, 2) + math.pow(withZoom.z, 2));
        if(magnitude > 10 && magnitude < 40)
        {
            //  x & z smoothed for zoom because y is smoothed for everything later
            //  This prevents jumpy camera movement when zooming
            currentOffset = new float3(
                math.lerp(currentOffset.x, withZoom.x, 0.1f),
                withZoom.y,
                math.lerp(currentOffset.z, withZoom.z, 0.1f)
            );
        }

        //  New position and rotation without any smoothing
        float3 newPosition      = playerPosition + currentOffset;
        Quaternion newRotation  = Quaternion.LookRotation(playerPosition - (float3)oldPosition, Vector3.up);

        //  Smooth y for everything
        //  Movement depends on camera angle and lerping x & z causes camera
        //  to turn when moving, so we only smooth y for movement
        float yLerp = math.lerp(oldPosition.y, newPosition.y, 0.1f);

        //  Apply new position and rotation softly
        camera.transform.position = new float3(newPosition.x, yLerp, newPosition.z);
        camera.transform.rotation = Quaternion.Lerp(oldRotation, newRotation, 0.1f);
    }
}