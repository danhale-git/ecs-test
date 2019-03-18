﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using MyComponents;

public static class CustomDebugTools
{
    public static Dictionary<string, string> debugText = new Dictionary<string, string>();
    public static Dictionary<string, int> debugCounts = new Dictionary<string, int>();

    public static Matrix<Entity> currentMatrix;

    public static void SetDebugText(string key, string value)
    {
        debugText[key] = value;
    }
    public static void SetDebugText(string key, int value)
    {
        debugCounts[key] = value;
    }
    public static void IncrementDebugCount(string key)
    {
        int currenCount = 0;
        debugCounts.TryGetValue(key, out currenCount);
        debugCounts[key] = currenCount + 1;
    }

    public static string PrintMatrix(Matrix<Entity> matrix)
    {
        string mat = "";

        for(int z = matrix.width-1; z >= 0; z--)
        {
            for(int x = 0; x < matrix.width; x++)
            {
                int index = matrix.PositionToIndex(new float3(x, 0, z));
                mat += matrix.ItemIsSet(index) ? "x " : "o ";
            }
            mat += '\n';
        }
        return mat;
    }

    static Vector3[] cubeVectors = CubeVectors();
    public static List<Dictionary<Entity, List<DebugLine>>> mapSquareLines = new List<Dictionary<Entity, List<DebugLine>>>()
    {
        new Dictionary<Entity, List<DebugLine>>(),  //  Horizontal Buffer
        new Dictionary<Entity, List<DebugLine>>(),  //  Block buffer
        new Dictionary<Entity, List<DebugLine>>(),  //  Mark error
        new Dictionary<Entity, List<DebugLine>>()   //  Draw buffer
    };
    public static List<DebugLine> lines = new List<DebugLine>();

    public static int squareWidth = TerrainSettings.mapSquareWidth;
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
    public static void HorizontalBufferDebug(Entity entity)
    {
        EntityManager manager = World.Active.GetOrCreateManager<EntityManager>();

        Color color;

        int buffer = 0;

        if(manager.HasComponent<Tags.InnerBuffer>(entity)) buffer = 1;
        if(manager.HasComponent<Tags.OuterBuffer>(entity)) buffer = 2;
        if(manager.HasComponent<Tags.EdgeBuffer>(entity)) buffer = 3;


        switch(buffer)
        {
            case 0: color = new Color(1, 1, 1, 0.2f); break;
            case 1: color = new Color(1, 0, 0, 0.2f); break;
            case 2: color = new Color(0, 1, 0, 0.2f); break;
            case 3: color = new Color(0, 0, 1, 0.2f); break;
            default: return;
        }

        float3 pos = manager.GetComponentData<Translation>(entity).Value;
        List<DebugLine> rect = CreateBox(
            new float3(pos.x, 0, pos.z),
            squareWidth * 0.95f,
            color,
            0,
            0,
            noSides: true,
            topOnly: true
        );

        mapSquareLines[0][entity] = rect;
    }

    //  allLines[1]
    public static void BlockBufferDebug(Entity entity, MapSquare mapSquare)
    {
        EntityManager manager = World.Active.GetOrCreateManager<EntityManager>();

        float3 pos = manager.GetComponentData<Translation>(entity).Value;
        List<DebugLine> blockBufferRects = CreateBox(
            new float3(pos.x, 0, pos.z),
            squareWidth * 0.99f,
            new Color(0.8f, 0.8f, 0.8f, 0.1f),
            mapSquare.topBlockBuffer,
            mapSquare.bottomBlockBuffer,
            noSides: false
        );

        mapSquareLines[1][entity] = blockBufferRects;
    }
    //  allLines[3]
    public static void DrawBufferDebug(Entity entity, MapSquare mapSquare)
    {
        EntityManager manager = World.Active.GetOrCreateManager<EntityManager>();

        float3 pos = manager.GetComponentData<Translation>(entity).Value;
        List<DebugLine> blockBufferRects = CreateBox(
            new float3(pos.x, 0, pos.z),
            squareWidth * 0.99f,
            new Color(0.8f, 0.8f, 0.8f, 0.1f),
            mapSquare.topDrawBounds,
            mapSquare.bottomDrawBounds,
            noSides: false
        );

        mapSquareLines[3][entity] = blockBufferRects;
    }

    //  allLines[2]
    public static void MarkError(Entity entity, Color color)
    {
        if(color == null) color = Color.red;
        EntityManager manager = World.Active.GetOrCreateManager<EntityManager>();

        float3 pos = manager.GetComponentData<Translation>(entity).Value;
        List<DebugLine> errorCuboid = CreateBox(
            new float3(pos.x, 0, pos.z),
            squareWidth,
            color,
            TerrainSettings.terrainHeight,
            0,
            noSides: false
        );

        mapSquareLines[2][entity] = errorCuboid;
    }

    //  allLines[2]
    public static void MarkError(float3 position, Color color)
    {
        EntityManager manager = World.Active.GetOrCreateManager<EntityManager>();

        List<DebugLine> errorCuboid = CreateBox(
            new float3(position.x, 0, position.z),
            squareWidth,
            color,
            TerrainSettings.terrainHeight,
            0,
            noSides: false
        );

        mapSquareLines[2][manager.CreateEntity()] = errorCuboid;
    }

    //  allLines[2]
    public static void MarkError(float3 position, Color color, EntityCommandBuffer commandBuffer)
    {
        List<DebugLine> errorCuboid = CreateBox(
            new float3(position.x, 0, position.z),
            squareWidth,
            color,
            TerrainSettings.terrainHeight,
            0,
            noSides: false
        );

        mapSquareLines[2][commandBuffer.CreateEntity()] = errorCuboid;
    }

    public static void Line(float3 start, float3 end, Color color)
    {
        lines.Add(new DebugLine(start, end, color));
    }

    static List<DebugLine> CreateBox(Vector3 position, float size, Color color, float top, float bottom, bool noSides = false, bool topOnly = false)
    {
        //  Adjust height for generated/non generated squares
        Vector3 topOffset = new Vector3(0, top, 0);
        Vector3 bottomOffset = new Vector3(0, bottom, 0);
     
        Vector3[] v = new Vector3[cubeVectors.Length];
        //  Offset to center cubes smaller than squareWidth
        Vector3 offsetAll = position;// + (Vector3.one * ((squareWidth - size)/2));
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

    public static GameObject Cube(Color color, Vector3 worldPosition, int scale = 1)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.GetComponent<Renderer>().material.color = color;
        cube.transform.localScale = new float3(scale);
        cube.transform.position = worldPosition;
        return cube;
    }

    public static Color NoiseToColor(float noise)
    {
        if(noise < 0.1f) return Color.black;
        else if(noise < 0.2f) return Color.blue;
        else if(noise < 0.3f) return Color.clear;
        else if(noise < 0.4f) return Color.cyan;
        else if(noise < 0.5f) return Color.gray;
        else if(noise < 0.6f) return Color.green;
        else if(noise < 0.7f) return Color.grey;
        else if(noise < 0.8f) return Color.magenta;
        else if(noise < 0.9f) return Color.red;
        else return Color.yellow;
    }
}
