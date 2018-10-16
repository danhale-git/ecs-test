using System.Collections;
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
    }
}
