using Sarabande.Levels;
using System.Collections.Generic;
using UnityEngine;

public class StaticListenerFactory : MonoBehaviour
{
    [Header("GameObject Folders")]
    private Transform fakeWallsParent;

    [Header("Prefabs")]
    [SerializeField] private GameObject fakeWallPrefab;


    [SerializeField] private bool jobDone = false;
    public bool IsJobDone() { return jobDone; }

    public void BuildStaticListeners(LevelData _levelData)
    {
        if (!PrefabAreValid()) return;

        CreateFolders();

        BuildPassThroughWalls(_levelData);
        //message
        //arrowtrap
        //doors
        //tiles

        jobDone = true;
    }
    private bool PrefabAreValid()
    {
        if (fakeWallPrefab == null)
        {
            Debug.LogError("Missing Prefabs");
            return false;
        }

        bool valid = true;
        if (fakeWallPrefab.GetComponent<FakeWallEntity>() == null)
        {
            Debug.LogError("FakeWallPrefab has no FakeWallEntity attached");
            valid = false;
        }

        return valid;
    }

    private void CreateFolders()
    {
        fakeWallsParent = new GameObject("FakeWalls").transform;
        fakeWallsParent.SetParent(transform, false);
    }

    private void BuildPassThroughWalls(LevelData _levelData)
    {
        if (_levelData.passThroughWalls == null) return;

        if (fakeWallPrefab.GetComponent<FakeWallEntity>() == null)
        {
            Debug.LogError("Fake Wall Prefab has no FakeWallEntity attached");
            return;
        }

        // dé-duplication légère au cas où
        var set = new HashSet<(int x, int z)>();

        foreach (var coord in _levelData.passThroughWalls)
        {
            if (!set.Add((coord.x, coord.z)))
            {
                Debug.LogWarning($"[LevelLoader] Doublon passThrough ignoré en ({coord.x},{coord.z}).");
                continue;
            }

            GameObject temp = Instantiate(fakeWallPrefab, fakeWallsParent);
            FakeWallEntity entity = temp.GetComponent<FakeWallEntity>();

            entity.Init(coord, $"FakeWall_{coord.ToString()}");

        }
    }
}
