using Sarabande.Core;
using Sarabande.Levels;
using System.Collections.Generic;
using UnityEngine;

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
        BuildWalls(_levelData);
        BuildThinWalls(_levelData);

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

    private void BuildWalls(LevelData _levelData)
    {

        // Dé-duplication pour éviter les doublons saisis par erreur
        var set = new HashSet<(int x, int z)>();

        foreach (var coord in _levelData.nonWalkables)
        {
            if (!set.Add((coord.x, coord.z)))
            {
                Debug.LogWarning($"[LevelLoader] Doublon nonWalkable ignoré en ({coord.x},{coord.z})."); continue;
            }

            GameObject temp = Instantiate(wallPrefab, wallsParent);
            WallVisual visual = temp.GetComponent<WallVisual>();

            visual.Init(coord, $"Wall_{coord.ToString()}");

        }
    }

    private void BuildThinWalls(LevelData _levelData)
    {
        // Dé-duplication (un même segment ajouté deux fois)
        var seen = new HashSet<(int ax, int az, int bx, int bz)>();

        foreach (var edgeCoord in _levelData.thinWalls)
        {
            // Normaliser l'ordre pour la HashSet
            var key = (ax: Mathf.Min(edgeCoord.a.x, edgeCoord.b.x), az: Mathf.Min(edgeCoord.a.z, edgeCoord.b.z),
                       bx: Mathf.Max(edgeCoord.a.x, edgeCoord.b.x), bz: Mathf.Max(edgeCoord.a.z, edgeCoord.b.z));

            if (!seen.Add(key))
            {
                Debug.LogWarning($"[LevelLoader] Doublon thinWall ignoré entre ({edgeCoord.a.x},{edgeCoord.a.z}) et ({edgeCoord.b.x},{edgeCoord.b.z}).");
                continue;
            }

            // Vérification adjacency (même x ou même z, distance 1)
            bool sameX = edgeCoord.a.x == edgeCoord.b.x && Mathf.Abs(edgeCoord.a.z - edgeCoord.b.z) == 1;
            bool sameZ = edgeCoord.a.z == edgeCoord.b.z && Mathf.Abs(edgeCoord.a.x - edgeCoord.b.x) == 1;
            bool vertical = sameX;

            if (!sameX && !sameZ)
            {
                Debug.LogError($"[LevelLoader] thinWall non-adjacent entre ({edgeCoord.a.x},{edgeCoord.a.z}) et ({edgeCoord.b.x},{edgeCoord.b.z})");
                continue;
            }

            GameObject temp = Instantiate(thinWallPrefab, Vector3.zero, Quaternion.identity, thinWallsParent);
            ThinWallVisual visual = temp.GetComponent<ThinWallVisual>();

            visual.Init(edgeCoord, $"Thin_{edgeCoord.a.ToString()}_{edgeCoord.b.ToString()}", vertical);

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
