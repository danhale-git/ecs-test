using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using UnityEngine.UI;

public class DebugMonoB : MonoBehaviour
{
    public Text debugPanelText;
    GameObject canvas;

    DebugSystem debugSystem;

    void Start()
    {
        canvas = FindObjectOfType<Canvas>().gameObject;
        debugSystem = World.Active.GetOrCreateManager<DebugSystem>();
    }

    void Update()
    {
        UpdateDebugPanel();
    }

    void UpdateDebugPanel()
    {
        Dictionary<string, string> stringsCopy = new Dictionary<string, string>(DebugTools.debugText);
        string newText = "";
        foreach(KeyValuePair<string, string> kvp in DebugTools.debugText)
        {
            newText += kvp.Key+": "+kvp.Value+"\n";
        }
        foreach(KeyValuePair<string, int> kvp in DebugTools.debugCounts)
        {
            newText += kvp.Key+": "+kvp.Value.ToString()+"\n";
        }
        debugPanelText.text = newText;
    }

    void OnDrawGizmos()
    {
        if(!Application.isPlaying) return;

        EntityManager manager = World.Active.GetOrCreateManager<EntityManager>();

        for(int i = 0; i < debugSystem.mapSquareLines.Count; i++)
        {
            Dictionary<Entity, List<DebugLineUtil.DebugLine>> dict = debugSystem.mapSquareLines[i];
            Dictionary<Entity, List<DebugLineUtil.DebugLine>> dictCopy = new Dictionary<Entity, List<DebugLineUtil.DebugLine>>();
            foreach(KeyValuePair<Entity, List<DebugLineUtil.DebugLine>> kvp in dict)
            {
                if(manager.Exists(kvp.Key))
                {
                    dictCopy.Add(kvp.Key, kvp.Value);
                    foreach(DebugLineUtil.DebugLine line in kvp.Value)
                    {
                        Gizmos.color = line.c;
                        Gizmos.DrawLine(line.a, line.b);
                    }
                }
            }

            debugSystem.mapSquareLines[i] = dictCopy;
        }
    } 
}
