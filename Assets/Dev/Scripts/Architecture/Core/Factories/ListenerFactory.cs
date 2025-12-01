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
    private Transform leversParent;
    private Transform listenerFolder;

    [Header("Prefabs")]
    [SerializeField] private GameObject fakeWallPrefab;
    [SerializeField] private GameObject messagePrefab;
    [SerializeField] private GameObject pressurePadPrefab;
    [SerializeField] private GameObject leverPrefab;


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
        CreateTriggerObjects(_levelData);
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

    private void CreateTriggerObjects(LevelData _levelData)
    {

        foreach (TriggerObjectConfig config in _levelData.triggerObjects)
        {
            if(config.type == TriggerObjectType.TriggerPad)
            {
                GameObject temp = Instantiate(pressurePadPrefab, pressurePadsParent);
                TriggerPadEntity entity = temp.GetComponent<TriggerPadEntity>();

                entity.Init(config, $"PressurePad_{config.cell.ToString()}");
            }

            if(config.type == TriggerObjectType.Lever)
            {
                GameObject temp = Instantiate(leverPrefab, leversParent);
                LeverEntity entity = temp.GetComponent<LeverEntity>();

                entity.Init(config, $"Lever_{config.cell.ToString()}");
            }


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


        leversParent = new GameObject("Levers").transform;
        leversParent.SetParent(listenerFolder, false);
    }

    private void CreateFakeWalls(LevelData _levelData)
    {
        if (_levelData.fakeWalls == null) return;

        // dé-duplication légère au cas où
        var set = new HashSet<(int x, int y)>();

        foreach (var coord in _levelData.fakeWalls)
        {
            if (!set.Add((coord.x, coord.y)))
            {
                Debug.LogWarning($"[LevelLoader] Doublon passThrough ignoré en ({coord}).");
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
