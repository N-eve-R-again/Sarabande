using Sarabande.Core;
using Sarabande.Levels;
using System.Collections.Generic;
using System.Drawing;
using UnityEngine;

public class StaticListenerFactory : MonoBehaviour, IClearable
{
    [Header("GameObject Folders")]
    private Transform fakeWallsParent;
    private Transform messageParent;

    [Header("Prefabs")]
    [SerializeField] private GameObject fakeWallPrefab;
    [SerializeField] private GameObject messagePrefab;


    [SerializeField] private bool jobDone = false;
    public bool IsJobDone() { return jobDone; }
    public void ClearObject()
    {

    }
    public void BuildStaticListeners(LevelData _levelData)
    {
        if (!PrefabAreValid()) return;

        CreateFolders();

        CreateFakeWalls(_levelData);
        CreateMessages(_levelData);
        //message
        //arrowtrap
        //doors
        //tiles

        jobDone = true;
    }
    private bool PrefabAreValid()
    {

        if (fakeWallPrefab == null || messagePrefab == null)
        {
            Debug.LogError("Missing one or all Prefabs");
            return false;
        }

        bool valid = true;

        if (fakeWallPrefab.GetComponent<FakeWallEntity>() == null)
        {
            Debug.LogError("FakeWallPrefab has no FakeWallEntity attached");
            valid = false;
        }

        if (fakeWallPrefab.GetComponent<FakeWallEntity>() == null)
        {
            Debug.LogError("MessagePrefab has no MessageCollectibleEntity attached");
            valid = false;
        }


        return valid;
    }

    private void CreateFolders()
    {
        fakeWallsParent = new GameObject("FakeWalls").transform;
        fakeWallsParent.SetParent(transform, false);

        messageParent = new GameObject("MessagesCollectibles").transform;
        messageParent.SetParent(transform, false);
    }

    private void CreateFakeWalls(LevelData _levelData)
    {
        if (_levelData.passThroughWalls == null) return;

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

    private void CreateMessages(LevelData levelData)
    {
        if (levelData.messages == null) return;

        foreach(MessageSpec msg in levelData.messages)
        {
            GameObject temp = Instantiate(messagePrefab, messageParent);
            MessageCollectibleEntity entity = temp.GetComponent<MessageCollectibleEntity>();

            entity.Init(msg, $"Message_{msg.cell.ToString()}");
        }

    }
}
