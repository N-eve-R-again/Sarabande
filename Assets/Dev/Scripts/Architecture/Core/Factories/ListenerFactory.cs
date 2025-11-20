using Sarabande.Core;
using Sarabande.Levels;
using System.Collections.Generic;
using System.Drawing;
using UnityEngine;

public class ListenerFactory : MonoBehaviour, IClearable
{
    [Header("GameObject Folders")]
    private Transform fakeWallsParent;
    private Transform messagesParent;
    private Transform pressurePadsParent;
    private Transform listenerFolder;

    [Header("Prefabs")]
    [SerializeField] private GameObject fakeWallPrefab;
    [SerializeField] private GameObject messagePrefab;
    [SerializeField] private GameObject pressurePadPrefab;


    [SerializeField] private bool jobDone = false;
    public bool IsJobDone() => jobDone;
    public void ClearObject()
    {
        //supprimer tout les objets
        
    }

    public void BuildListeners(LevelData _levelData)
    {
        if (!PrefabAreValid()) return;

        CreateFolders();

        CreateFakeWalls(_levelData);
        CreateMessages(_levelData);
        CreatePressurePads(_levelData);
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
        return valid;
    }

    private void CreatePressurePads(LevelData _levelData)
    {
        if (_levelData.arrowTraps == null) return;

        foreach (TriggerPadConfig config in _levelData.newTriggerPads)
        {

            GameObject temp = Instantiate(pressurePadPrefab, pressurePadsParent);
            TriggerPadEntity entity = temp.GetComponent<TriggerPadEntity>();

            entity.Init(config, $"PressurePad_{config.cell.ToString()}");

        }
    }

    private void CreateFolders()
    {
        listenerFolder = new GameObject("Listeners").transform;
        listenerFolder.SetParent(transform.parent);

        fakeWallsParent = new GameObject("FakeWalls").transform;
        fakeWallsParent.SetParent(listenerFolder, false);

        messagesParent = new GameObject("MessagesCollectibles").transform;
        messagesParent.SetParent(listenerFolder, false);

        pressurePadsParent = new GameObject("PressurePads").transform;
        pressurePadsParent.SetParent(listenerFolder, false);
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

        foreach(MessageConfig msg in levelData.messages)
        {
            GameObject temp = Instantiate(messagePrefab, messagesParent);
            MessageCollectibleEntity entity = temp.GetComponent<MessageCollectibleEntity>();

            entity.Init(msg, $"Message_{msg.cell.ToString()}");
        }

    }
}
