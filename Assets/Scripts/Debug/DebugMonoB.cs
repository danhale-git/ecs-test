using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using UnityEngine.UI;

public class DebugMonoB : MonoBehaviour
{
    public Text debugPanelText;

    void Update()
    {
        string newText = "";
        foreach(KeyValuePair<string, string> kvp in CustomDebugTools.debugText)
        {
            newText += kvp.Key+": "+kvp.Value+"\n";
        }
        foreach(KeyValuePair<string, int> kvp in CustomDebugTools.debugCounts)
        {
            newText += kvp.Key+": "+kvp.Value.ToString()+"\n";
        }

        debugPanelText.text = newText;
    }

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
