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

        for(int i = 0; i < CustomDebugTools.allLines.Count; i++)
        {
            Dictionary<Entity, List<CustomDebugTools.DebugLine>> dict = CustomDebugTools.allLines[i];
            Dictionary<Entity, List<CustomDebugTools.DebugLine>> dictCopy = new Dictionary<Entity, List<CustomDebugTools.DebugLine>>();
            foreach(KeyValuePair<Entity, List<CustomDebugTools.DebugLine>> kvp in dict)
            {
                if(manager.Exists(kvp.Key))
                {
                    dictCopy.Add(kvp.Key, kvp.Value);
                    foreach(CustomDebugTools.DebugLine line in kvp.Value)
                    {
                        Gizmos.color = line.c;
                        Gizmos.DrawLine(line.a, line.b);
                    }
                }
            }

            CustomDebugTools.allLines[i] = dictCopy;
        }
    }
}
