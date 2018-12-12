using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DebugMonoB : MonoBehaviour
{
    void Awake()
    {
        CustomDebugTools.lines.Clear();
    }

    void OnDrawGizmos()
    {
        if(!Application.isPlaying) return;
        foreach(CustomDebugTools.DebugLine line in CustomDebugTools.lines)
		{
			Gizmos.color = line.c;
			Gizmos.DrawLine(line.a, line.b);
		}

        foreach(KeyValuePair<Vector3, List<CustomDebugTools.DebugLine>> kvp in CustomDebugTools.linesDict)
        {
            foreach(CustomDebugTools.DebugLine line in kvp.Value)
            {
                Gizmos.color = line.c;
                Gizmos.DrawLine(line.a, line.b);
            }
        }
    }
}
