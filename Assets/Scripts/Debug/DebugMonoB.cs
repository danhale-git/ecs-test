using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using UnityEngine.UI;

public class DebugMonoB : MonoBehaviour
{
    public Text debugPanelText;
    public GameObject squarePrefab;
    public List<GameObject> allSquares = new List<GameObject>();
    GameObject canvas;

    float timer = 0;

    void Start()
    {
        canvas = FindObjectOfType<Canvas>().gameObject;
    }

    public void DebugMatrixEntities()
    {
        GridMatrix<Entity> matrix = CustomDebugTools.currentMatrix;
        Vector2 centerOffset = new Vector2(Screen.width/2, Screen.height/2);

        foreach(GameObject square in allSquares)
            Destroy(square);

        for(int i = 0; i < matrix.Length; i++)
        {
            Vector3 position = (matrix.FlatIndexToGridPosition(i) * 15) / matrix.gridSquareSize;
            Vector2 position2 = new Vector2(position.x, position.z) + centerOffset;
            Color color;
            if(matrix.ItemIsSet(i))
            {
                color = new Color(1, 0, 0, 0.5f);
            }
            else
            {
                color = new Color(0, 1, 1, 0.5f);
            } 
            GameObject square = Instantiate(squarePrefab, position2, Quaternion.identity, canvas.transform);

            square.GetComponent<Image>().color = color;

            allSquares.Add(square);
        }    
    }

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

        if(Time.fixedTime > timer+1)
        {
            DebugMatrixEntities();
            timer = Time.fixedTime;
        }
    }

    void OnDrawGizmos()
    {
        if(!Application.isPlaying) return;

        EntityManager manager = World.Active.GetOrCreateManager<EntityManager>();

        for(int i = 0; i < CustomDebugTools.mapSquareLines.Count; i++)
        {
            Dictionary<Entity, List<CustomDebugTools.DebugLine>> dict = CustomDebugTools.mapSquareLines[i];
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

            CustomDebugTools.mapSquareLines[i] = dictCopy;
        }

        foreach(CustomDebugTools.DebugLine line in CustomDebugTools.lines)
        {
            Gizmos.color = line.c;
            Gizmos.DrawLine(line.a, line.b);
        }
    }
}
