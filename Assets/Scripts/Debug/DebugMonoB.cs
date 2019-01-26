using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;

public class DebugMonoB : MonoBehaviour
{

    void OnDrawGizmos()
    {
        if(!Application.isPlaying) return;

        EntityManager manager = World.Active.GetOrCreateManager<EntityManager>();

        foreach(Dictionary<Entity, List<CustomDebugTools.DebugLine>> dict in CustomDebugTools.allLines)
        {
            foreach(KeyValuePair<Entity, List<CustomDebugTools.DebugLine>> kvp in dict)
            {
                if(!manager.Exists(kvp.Key))
                {
                    dict.Remove(kvp.Key);
                    continue;
                }
                foreach(CustomDebugTools.DebugLine line in kvp.Value)
                {
                    Gizmos.color = line.c;
			        Gizmos.DrawLine(line.a, line.b);
                }
            }
        }
    }
}
