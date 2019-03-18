using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using MyComponents;

public static class DebugTools
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
