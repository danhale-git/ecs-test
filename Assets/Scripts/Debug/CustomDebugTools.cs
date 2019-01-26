using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using MyComponents;

public static class CustomDebugTools
{
    static Vector3[] cubeVectors = CubeVectors();
    public static List<Dictionary<Entity, List<DebugLine>>> allLines = new List<Dictionary<Entity, List<DebugLine>>>()
    {
        new Dictionary<Entity, List<DebugLine>>(),  //  Horizontal Buffer
        new Dictionary<Entity, List<DebugLine>>(),  
        new Dictionary<Entity, List<DebugLine>>()
    };

    public static int cubeSize = TerrainSettings.cubeSize;
    public struct DebugLine
    {
        public readonly Vector3 a, b;
        public readonly Color c;
        public DebugLine(Vector3 a, Vector3 b, Color c)
        {
            this.a = a;
            this.b = b;
            this.c = c;
        }
    }

    //  allLines[0]
    public static void HorizontalBufferDebug(Entity entity, int buffer)
    {
        EntityManager manager = World.Active.GetOrCreateManager<EntityManager>();

        Color color;

        switch(buffer)
        {
            case 1: color = new Color(1, 0, 0, 0.2f); break;
            case 2: color = new Color(0, 1, 0, 0.2f); break;
            case 3: color = new Color(0, 0, 1, 0.2f); break;
            default: return;
        }

        float3 pos = manager.GetComponentData<Position>(entity).Value;
        List<DebugLine> rect = CreateBox(
            new float3(pos.x, 0, pos.z),
            cubeSize * 0.95f,
            color,
            0,
            0,
            noSides: true,
            topOnly: true
        );

        allLines[0][entity] = rect;
    }

    //  allLines[1]
    public static void VerticalBufferDebug(Entity entity, MapSquare mapSquare)
    {
        EntityManager manager = World.Active.GetOrCreateManager<EntityManager>();

        float3 pos = manager.GetComponentData<Position>(entity).Value;
        List<DebugLine> blockBufferRects = CreateBox(
            new float3(pos.x, 0, pos.z),
            cubeSize * 0.99f,
            new Color(1, 0.3f, 0f, 0.1f),
            mapSquare.topBlockBuffer,
            mapSquare.bottomBlockBuffer,
            noSides: false
        );

        allLines[1][entity] = blockBufferRects;
    }

    //  allLines[2]
    public static void MarkError(Entity entity)
    {
        EntityManager manager = World.Active.GetOrCreateManager<EntityManager>();

        float3 pos = manager.GetComponentData<Position>(entity).Value;
        List<DebugLine> errorCuboid = CreateBox(
            new float3(pos.x, 0, pos.z),
            cubeSize,
            new Color(1, 0, 0),
            TerrainSettings.terrainHeight+TerrainSettings.terrainStretch,
            0,
            noSides: false
        );

        allLines[2][entity] = errorCuboid;
    }

    static List<DebugLine> CreateBox(Vector3 position, float size, Color color, float top, float bottom, bool noSides = false, bool topOnly = false)
    {
        //  Adjust height for generated/non generated squares
        Vector3 topOffset = new Vector3(0, top, 0);
        Vector3 bottomOffset = new Vector3(0, bottom, 0);
     
        Vector3[] v = new Vector3[cubeVectors.Length];
        //  Offset to center cubes smaller than cubeSize
        Vector3 offsetAll = position;// + (Vector3.one * ((cubeSize - size)/2));
        for(int i = 0; i < cubeVectors.Length; i++)
        {
            //  Set size and offset
            Vector3 vector = ((cubeVectors[i] * size) + offsetAll);
            v[i] = vector;
        }

        List<DebugLine> lines = new List<DebugLine>();
        
        //  Top square
        lines.Add(new DebugLine(v[4] + topOffset, v[6] + topOffset, color));
        lines.Add(new DebugLine(v[5] + topOffset, v[7] + topOffset, color));
        lines.Add(new DebugLine(v[4] + topOffset, v[7] + topOffset, color));
        lines.Add(new DebugLine(v[5] + topOffset, v[6] + topOffset, color));

        if(topOnly) return lines;

        //  Bottom square
        lines.Add(new DebugLine(v[0] + bottomOffset, v[2] + bottomOffset, color));
        lines.Add(new DebugLine(v[1] + bottomOffset, v[3] + bottomOffset, color));
        lines.Add(new DebugLine(v[0] + bottomOffset, v[3] + bottomOffset, color));
        lines.Add(new DebugLine(v[1] + bottomOffset, v[2] + bottomOffset, color));

        if(noSides) return lines;

        //  Connecting lines at corners
        lines.Add(new DebugLine(v[0] + bottomOffset, v[4] + topOffset, color));
        lines.Add(new DebugLine(v[1] + bottomOffset, v[5] + topOffset, color));
        lines.Add(new DebugLine(v[2] + bottomOffset, v[6] + topOffset, color));
        lines.Add(new DebugLine(v[3] + bottomOffset, v[7] + topOffset, color));

        return lines;
    }

    public static Vector3[] CubeVectors()
    {
        return new Vector3[] {  new Vector3(0, 0, 1),	//	left front bottom
	                            new Vector3(1, 0, 0),	//	right back bottom
	                            new Vector3(0, 0, 0), 	//	left back bottom
	                            new Vector3(1, 0, 1),	//	right front bottom
	                            new Vector3(0, 0, 1),	//	left front top
	                            new Vector3(1, 0, 0),	//	right back top
	                            new Vector3(0, 0, 0),	//	left back top
	                            new Vector3(1, 0, 1) };	//	right front top
    }

    public static void Cube(Color color, Vector3 worldPosition)
    {
        GameObject capsule = GameObject.CreatePrimitive(PrimitiveType.Cube);
        capsule.GetComponent<Renderer>().material.color = color;
        capsule.transform.position = worldPosition;
    }
}
