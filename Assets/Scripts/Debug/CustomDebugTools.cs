using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;

public static class CustomDebugTools
{
    static Vector3[] cubeVectors = Util.CubeVectorsPointFiveOffset();
    public static List<DebugLine> lines = new List<DebugLine>();
    public static Dictionary<Vector3, List<DebugLine>> squareHighlights = new Dictionary<Vector3, List<DebugLine>>();
    public static Dictionary<Vector3, List<DebugLine>> cubeHighlights = new Dictionary<Vector3, List<DebugLine>>();
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

    public static void SetMapSquareHighlight(Entity mapSquareEntity, int size, Color color)
    {
        Vector3 topOffset;

        EntityManager manager = World.Active.GetOrCreateManager<EntityManager>();

        //  Get position and offset to square corner
        Vector3 position = (Vector3)manager.GetComponentData<Position>(mapSquareEntity).Value - (Vector3.one / 2);
        //  Get MapSquare component
        MyComponents.MapSquare mapSquare = manager.GetComponentData<MyComponents.MapSquare>(mapSquareEntity);
        
        //  Adjust height for generated/non generated squares
        if( manager.HasComponent(mapSquareEntity, typeof(Tags.InnerBuffer)) ||
            manager.HasComponent(mapSquareEntity, typeof(Tags.OuterBuffer)))
        {
            topOffset = new Vector3(0, TerrainSettings.terrainHeight - cubeSize, 0);
        }
        else
        {
            topOffset = new Vector3(0, ((int)math.floor((mapSquare.highestVisibleBlock + 1) / cubeSize) * cubeSize) - cubeSize, 0);
        }
        
        Vector3[] v = new Vector3[cubeVectors.Length];
        //  Offset to center cubes smaller than cubeSize
        Vector3 offsetAll = position + (Vector3.one * ((cubeSize - size)/2));
        for(int i = 0; i < cubeVectors.Length; i++)
        {
            //  Set size and offset
            Vector3 vector = ((cubeVectors[i] * size) + offsetAll);
            v[i] = vector;
        }

        squareHighlights[position] = new List<DebugLine>();
        
        //  Top square
        squareHighlights[position].Add(new DebugLine(v[4] + topOffset, v[6] + topOffset, color));
        squareHighlights[position].Add(new DebugLine(v[5] + topOffset, v[7] + topOffset, color));
        squareHighlights[position].Add(new DebugLine(v[4] + topOffset, v[7] + topOffset, color));
        squareHighlights[position].Add(new DebugLine(v[5] + topOffset, v[6] + topOffset, color));

        //  Bottom square
        squareHighlights[position].Add(new DebugLine(v[0], v[2], color));
        squareHighlights[position].Add(new DebugLine(v[1], v[3], color));
        squareHighlights[position].Add(new DebugLine(v[0], v[3], color));
        squareHighlights[position].Add(new DebugLine(v[1], v[2], color));

        //  Connecting lines at corners
        squareHighlights[position].Add(new DebugLine(v[0], v[4] + topOffset, color));
        squareHighlights[position].Add(new DebugLine(v[1], v[5] + topOffset, color));
        squareHighlights[position].Add(new DebugLine(v[2], v[6] + topOffset, color));
        squareHighlights[position].Add(new DebugLine(v[3], v[7] + topOffset, color));
    }

    public static void SetMapCubeHighlight(Entity mapSquareEntity, int yPos, int size, Color color)
    {
        EntityManager manager = World.Active.GetOrCreateManager<EntityManager>();

        //  Get position and offset to square corner
        Vector3 position = (Vector3)manager.GetComponentData<Position>(mapSquareEntity).Value;
        position = position - (Vector3.one / 2) + new Vector3(0, yPos, 0);
        //  Get MapSquare component
        MyComponents.MapSquare mapSquare = manager.GetComponentData<MyComponents.MapSquare>(mapSquareEntity);
                
        Vector3[] v = new Vector3[cubeVectors.Length];
        //  Offset to center cubes smaller than cubeSize
        Vector3 offsetAll = position + (Vector3.one * ((cubeSize - size)/2));
        for(int i = 0; i < cubeVectors.Length; i++)
        {
            //  Set size and offset
            Vector3 vector = ((cubeVectors[i] * size) + offsetAll);
            v[i] = vector;
        }

        cubeHighlights[position] = new List<DebugLine>();
        
        //  Top square
        cubeHighlights[position].Add(new DebugLine(v[4], v[6], color));
        cubeHighlights[position].Add(new DebugLine(v[5], v[7], color));
        cubeHighlights[position].Add(new DebugLine(v[4], v[7], color));
        cubeHighlights[position].Add(new DebugLine(v[5], v[6], color));

        //  Bottom square
        cubeHighlights[position].Add(new DebugLine(v[0], v[2], color));
        cubeHighlights[position].Add(new DebugLine(v[1], v[3], color));
        cubeHighlights[position].Add(new DebugLine(v[0], v[3], color));
        cubeHighlights[position].Add(new DebugLine(v[1], v[2], color));

        //  Connecting lines at corners
        cubeHighlights[position].Add(new DebugLine(v[0], v[4], color));
        cubeHighlights[position].Add(new DebugLine(v[1], v[5], color));
        cubeHighlights[position].Add(new DebugLine(v[2], v[6], color));
        cubeHighlights[position].Add(new DebugLine(v[3], v[7], color));
    }
}
