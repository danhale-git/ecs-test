using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public static class CustomDebugTools
{
    static Vector3[] cubeVectors = Util.CubeVectors();
    public static List<DebugLine> lines = new List<DebugLine>();
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
      
    public static void WireCube(Vector3 center, int size, Color color, bool _2D = false)
    {
        Vector3[] v = new Vector3[cubeVectors.Length];
        for(int i = 0; i < cubeVectors.Length; i++)
        {
            v[i] = (cubeVectors[i] * size) + center;
        }

        lines.Add(new DebugLine(v[4], v[6], color));
        lines.Add(new DebugLine(v[5], v[7], color));
        lines.Add(new DebugLine(v[4], v[7], color));
        lines.Add(new DebugLine(v[5], v[6], color));

        if(_2D) return;

        lines.Add(new DebugLine(v[0], v[2], color));
        lines.Add(new DebugLine(v[1], v[3], color));
        lines.Add(new DebugLine(v[0], v[3], color));
        lines.Add(new DebugLine(v[1], v[2], color));

        lines.Add(new DebugLine(v[0], v[4], color));
        lines.Add(new DebugLine(v[1], v[5], color));
        lines.Add(new DebugLine(v[2], v[6], color));
        lines.Add(new DebugLine(v[3], v[7], color));
    }
}
