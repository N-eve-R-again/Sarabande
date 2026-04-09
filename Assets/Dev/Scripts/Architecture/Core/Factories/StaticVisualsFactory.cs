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
    private Transform obstaclesFolder;

    private Transform ParentOfAll;

    [Header("Prefabs")]
    [SerializeField] private GameObject wallPrefab;
    [SerializeField] private GameObject thinWallPrefab;
    private int obstaclecount = 0;

    [Header("Grid Visuals")]
    [SerializeField, Min(0.001f)] private float lineWidth = 0.03f;
    [SerializeField] private float lineY = 0.01f; // décoller un peu du sol pour éviter le z-fighting
    [SerializeField] private Material lineMaterial;


    [SerializeField] private bool jobDone = false;
    public bool IsJobDone() => jobDone;
    public void ClearObject()
    {

    }
    public int BuildStaticVisuals(LevelData _levelData,Transform parent)
    {
        obstaclecount = 0;
        ParentOfAll = parent;

        CreateFolders();

        BuildGridLines(_levelData);
        BuildObstacles(_levelData);

        jobDone = true;
        return obstaclecount;

    }


    private void CreateFolders()
    {
        obstaclesFolder = new GameObject("Obstacles").transform;
        obstaclesFolder.SetParent(transform.parent);

        gridParent = new GameObject("GridLines").transform;
        wallsParent = new GameObject("Walls").transform;
        thinWallsParent = new GameObject("ThinWalls").transform;

        gridParent.SetParent(obstaclesFolder, false);
        wallsParent.SetParent(obstaclesFolder, false);
        thinWallsParent.SetParent(obstaclesFolder, false);

        obstaclesFolder.SetParent(ParentOfAll, true);
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
                obstaclecount++;
                GameObject temp = Instantiate(wallPrefab, wallsParent);
                WallEntity visual = temp.GetComponent<WallEntity>();

                visual.Init(obstacle, $"Wall_{obstacle.cell.ToString()}");
            }
            else
            {
                obstaclecount++;
                GameObject temp = Instantiate(thinWallPrefab, Vector3.zero, Quaternion.identity, thinWallsParent);
                ThinWallEntity visual = temp.GetComponent<ThinWallEntity>();

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
