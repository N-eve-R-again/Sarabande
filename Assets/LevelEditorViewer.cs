using Sarabande.Core;
using Sarabande.Levels;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;


public class LevelEditorViewer : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public LevelContext levelContext;
    public LevelData levelDataCopy;
    public Vector3 triggerpadsize = Vector3.one;
    [Range(0.05f,1f)]
    public float arrowtraplinesize = 1f;
    [Range(0.05f, 1f)]
    public float arrowtrapsize = 1f;
    public List<Vector3> walls = new List<Vector3>();
    public List<Vector3> triggerpads = new List<Vector3>();
    public List<Vector3> triggerpadsgoto = new List<Vector3>();
    public List<Vector3> arrowtraps = new List<Vector3>();
    public List<float> arrowtrapdirections = new List<float>();
    public Vector3[] bounds = new Vector3[4];
    public Dictionary<int,Vector3> links = new();


    // Update is called once per frame
    void OnDrawGizmos()
    {
        if (!this.enabled) return;
        Gizmos.color = Color.red;
        Gizmos.DrawLine(bounds[0], bounds[1]);
        Gizmos.DrawLine(bounds[1], bounds[2]);
        Gizmos.DrawLine(bounds[2], bounds[3]);
        Gizmos.DrawLine(bounds[3], bounds[0]);
        Gizmos.color = Color.white;
        foreach (var pos in walls)
        {
            Gizmos.DrawWireCube(pos, Vector3.one * LevelGlobalSettings.cellSize * 0.95f);
            
        }
        
        int i = 0;
        foreach (var pos in triggerpads)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawCube(pos, triggerpadsize * LevelGlobalSettings.cellSize);
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(pos, triggerpadsgoto[i]);
            i++;
        }

        i = 0;
        Gizmos.color = Color.magenta;
        foreach (var pos in arrowtraps)
        {
            Gizmos.DrawSphere(pos, arrowtrapsize);
            Gizmos.DrawLine(pos, pos + Quaternion.Euler(0, arrowtrapdirections[i], 0) * Vector3.forward * arrowtraplinesize);
            i++;
        }

    }

    public void ReImportLevel()
    {
        levelDataCopy = levelContext.LevelData;
        walls.Clear();
        triggerpads.Clear();
        arrowtraps.Clear();
        arrowtrapdirections.Clear();
        triggerpadsgoto.Clear();
        Vector3 offsety = Vector3.up * 0.2f;
        bounds[0] = offsety + Vector3.zero;
        bounds[1] = offsety + Vector3.forward * levelDataCopy.height;
        bounds[2] = offsety + (Vector3.forward + Vector3.right) * levelDataCopy.width;
        bounds[3] = offsety + Vector3.right * levelDataCopy.width;
        foreach (var item in levelDataCopy.nonWalkables)
        {
            walls.Add((Vector3.up * 0.5f) + GridUtils.Center(item, LevelGlobalSettings.cellSize));
        }

        foreach (var item in levelDataCopy.newArrowTraps)
        {
            Vector3 newpos = (Vector3.up * 0.5f) + GridUtils.Center(item.cell, LevelGlobalSettings.cellSize);
            newpos -= Quaternion.Euler(0, SetRotation(item.travelDir), 0) * Vector3.forward * 0.5f;
            arrowtraps.Add(newpos);
            links[item.triggerKey] = newpos;
            arrowtrapdirections.Add(SetRotation(item.travelDir));
        }

        foreach (var item in levelDataCopy.newTriggerPads)
        {
            triggerpads.Add((Vector3.up * triggerpadsize.y * 0.5f) + GridUtils.Center(item.cell, LevelGlobalSettings.cellSize));
            if(links.TryGetValue(item.triggerKey,out Vector3 link)){
                triggerpadsgoto.Add(link);
            }
        }
    }

    private float SetRotation(EdgeDirection dir)
    {
        float yRot = 0f;
        switch (dir)
        {
            case EdgeDirection.North:
                yRot = 0f; break;
            case EdgeDirection.South: yRot = 180f; break;
            case EdgeDirection.East: yRot = 90f; break;
            case EdgeDirection.West: yRot = 270f; break;

        }
        return yRot;
    }
}

[CustomEditor(typeof(LevelEditorViewer))]
public class YourScriptEditor : Editor
{

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        if (GUILayout.Button("Refresh"))
            test();
        
    }

    public void test()
    {
        target.GameObject().GetComponent<LevelEditorViewer>().ReImportLevel();
    }

}

