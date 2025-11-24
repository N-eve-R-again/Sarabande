using Sarabande.Core;
using Sarabande.Levels;
using System.Collections.Generic;
using System.Drawing;
using UnityEngine;
using static UnityEditor.Searcher.SearcherWindow.Alignment;

public class StaticVisualsFactory : MonoBehaviour, IClearable
{
    [Header("GameObject Folders")]
    private Transform gridParent;
    private Transform wallsParent;
    private Transform thinWallsParent;
    private Transform staticVisualsFolder;


    [Header("Prefabs")]
    [SerializeField] private GameObject wallPrefab;
    [SerializeField] private GameObject thinWallPrefab;

    [Header("Grid Visuals")]
    [SerializeField, Min(0.001f)] private float lineWidth = 0.03f;
    [SerializeField] private float lineY = 0.01f; // décoller un peu du sol pour éviter le z-fighting
    [SerializeField] private Material lineMaterial;


    [SerializeField] private bool jobDone = false;
    public bool IsJobDone() => jobDone;
    public void ClearObject()
    {

    }
    public void BuildStaticVisuals(LevelData _levelData)
    {
        if(!PrefabAreValid()) return;

        CreateFolders();

        BuildGridLines(_levelData);
        BuildObstacles(_levelData);

        jobDone = true;
    }

    private bool PrefabAreValid()
    {
        if(wallPrefab == null || thinWallPrefab == null)
        {
            Debug.LogError("Missing Prefabs");
            return false;
        }

        bool valid = true;
        if (wallPrefab.GetComponent<WallVisual>() == null)
        {
            Debug.LogError("WallPrefab has no WallVisual attached");
            valid = false;
        }
        if (thinWallPrefab.GetComponent<ThinWallVisual>() == null)
        {
            Debug.LogError("ThinWallPrefab has no ThinWallVisual attached");
            valid = false;
        }

        return valid;
    }
    private void CreateFolders()
    {
        staticVisualsFolder = new GameObject("Static Visuals").transform;
        staticVisualsFolder.SetParent(transform.parent);

        gridParent = new GameObject("GridLines").transform;
        wallsParent = new GameObject("Walls").transform;
        thinWallsParent = new GameObject("ThinWalls").transform;

        gridParent.SetParent(staticVisualsFolder, false);
        wallsParent.SetParent(staticVisualsFolder, false);
        thinWallsParent.SetParent(staticVisualsFolder, false);
    }

    private void BuildObstacles(LevelData _levelData)
    {

        // Dé-duplication pour éviter les doublons saisis par erreur
        var set = new HashSet<(int x, int y)>();

        foreach (var obstacle in _levelData.obstacles)
        {
            if (!set.Add((obstacle.cell.x, obstacle.cell.y)))
            {
                Debug.LogWarning($"[LevelLoader] Doublon nonWalkable ignoré en ({obstacle.cell.ToString()})."); continue;
            }

            if( obstacle.type == ObstacleData.ObstacleType.Wall)
            {
                GameObject temp = Instantiate(wallPrefab, wallsParent);
                WallVisual visual = temp.GetComponent<WallVisual>();

                visual.Init(obstacle, $"Wall_{obstacle.cell.ToString()}");
            }
            else
            {

                GameObject temp = Instantiate(thinWallPrefab, Vector3.zero, Quaternion.identity, thinWallsParent);
                ThinWallVisual visual = temp.GetComponent<ThinWallVisual>();

                visual.Init(obstacle, $"Thin_{obstacle.cell.ToString()}");
            }
        }
    }

  

    private void BuildGridLines(LevelData _levelData)
    {
        int w = _levelData.width;
        int h = _levelData.height;

        // Lignes verticales (x constant, z de 0 à h)
        for (int x = 0; x <= w; x++)
        {
            var go = new GameObject($"VLine_{x}");
            go.transform.SetParent(gridParent, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.useWorldSpace = true;
            lr.material = lineMaterial;
            lr.startWidth = lr.endWidth = lineWidth;
            lr.numCapVertices = 2; // bouts arrondis
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;

            lr.SetPosition(0, new Vector3(x * LevelGlobalSettings.cellSize, lineY, 0f));
            lr.SetPosition(1, new Vector3(x * LevelGlobalSettings.cellSize, lineY, h * LevelGlobalSettings.cellSize));
        }

        // Lignes horizontales (z constant, x de 0 à w)
        for (int z = 0; z <= h; z++)
        {
            var go = new GameObject($"HLine_{z}");
            go.transform.SetParent(gridParent, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.useWorldSpace = true;
            lr.material = lineMaterial;
            lr.startWidth = lr.endWidth = lineWidth;
            lr.numCapVertices = 2;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;

            lr.SetPosition(0, new Vector3(0f, lineY, z * LevelGlobalSettings.cellSize));
            lr.SetPosition(1, new Vector3(w * LevelGlobalSettings.cellSize, lineY, z * LevelGlobalSettings.cellSize));
        }
    }
}
