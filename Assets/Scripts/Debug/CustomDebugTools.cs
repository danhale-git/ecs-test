using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;

public static class CustomDebugTools
{
    static Vector3[] cubeVectors = CubeVectors();
    public static List<DebugLine> lines = new List<DebugLine>();
    public static Dictionary<Vector3, List<DebugLine>> squareHighlights = new Dictionary<Vector3, List<DebugLine>>();
    public static Dictionary<Vector3, List<DebugLine>> blockHighlights = new Dictionary<Vector3, List<DebugLine>>();
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

    public static void SetMapSquareHighlight(Entity mapSquareEntity, int size, Color color, int top, int bottom)
    {

        EntityManager manager = World.Active.GetOrCreateManager<EntityManager>();

        //  Get position and offset to square corner
        Vector3 pos = (Vector3)manager.GetComponentData<Position>(mapSquareEntity).Value - (Vector3.one / 2);
        Vector3 position = new Vector3(pos.x, -0.5f, pos.z);

        squareHighlights[position] = CreateBox(position, size, color, top, bottom);
    }

    public static void SetBlockHighlight(Vector3 position, Color color)
    {
        blockHighlights[position] = CreateBox(position, 1, color, position.y+1, position.y);
        
    }

    static List<DebugLine> CreateBox(Vector3 position, float size, Color color, float top, float bottom)
    {
        //  Adjust height for generated/non generated squares
        Vector3 topOffset = new Vector3(0, top, 0);
        Vector3 bottomOffset = new Vector3(0, bottom, 0);
     
        Vector3[] v = new Vector3[cubeVectors.Length];
        //  Offset to center cubes smaller than cubeSize
        Vector3 offsetAll = position + (Vector3.one * ((cubeSize - size)/2));
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

        //  Bottom square
        lines.Add(new DebugLine(v[0] + bottomOffset, v[2] + bottomOffset, color));
        lines.Add(new DebugLine(v[1] + bottomOffset, v[3] + bottomOffset, color));
        lines.Add(new DebugLine(v[0] + bottomOffset, v[3] + bottomOffset, color));
        lines.Add(new DebugLine(v[1] + bottomOffset, v[2] + bottomOffset, color));

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
}
