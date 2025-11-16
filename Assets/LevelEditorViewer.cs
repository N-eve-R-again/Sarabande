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

    public List<Vector3> walls = new List<Vector3>();
    public List<Vector3> triggerpads = new List<Vector3>();

    // Update is called once per frame
    void OnDrawGizmos()
    {
        if (!this.enabled) return;
        foreach (var pos in walls)
        {
            Gizmos.DrawWireCube(pos, Vector3.one * LevelGlobalSettings.cellSize * 0.95f);
            
        }
        foreach (var pos in triggerpads)
        {

        }

    }

    public void ReImportLevel()
    {
        walls.Clear();
        foreach (var item in levelContext.LevelData.nonWalkables)
        {
            walls.Add((Vector3.up * 0.5f) + GridUtils.Center(item, LevelGlobalSettings.cellSize));
        }
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

