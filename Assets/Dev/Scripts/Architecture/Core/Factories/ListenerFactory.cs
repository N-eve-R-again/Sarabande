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

        foreach (var item in _levelData.listeners)
        {
            switch (item)
            {
                case FakeWallData fakeWallData :
                    CreateFakeWall(fakeWallData);
                    break;
                case MessageConfig messageConfig :
                    CreateMessage(messageConfig);
                    break;
                case TriggerObjectConfig triggerObjectConfig :
                    CreateTriggerObject(triggerObjectConfig);
                    break;
            }
        }

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

    private void CreateTriggerObject(TriggerObjectConfig config)
    {

        switch (config.type)
        {
            case TriggerObjectType.InvisibleTrigger:
                break;

            case TriggerObjectType.TriggerPad:
                GameObject triggerpad = Instantiate(pressurePadPrefab, pressurePadsParent);
                triggerpad.GetComponent<TriggerPadEntity>().Init(config);
                break;

            case TriggerObjectType.Lever:
                GameObject lever = Instantiate(leverPrefab, leversParent);
                lever.GetComponent<LeverEntity>().Init(config);
                break;
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

    private void CreateFakeWall(FakeWallData fk)
    {
        GameObject temp = Instantiate(fakeWallPrefab, fakeWallsParent);
        FakeWallEntity entity = temp.GetComponent<FakeWallEntity>();
        entity.Init(fk);
    }

    private void CreateMessage(MessageConfig msg)
    {
        GameObject temp = Instantiate(messagePrefab, messagesParent);
        MessageCollectibleEntity entity = temp.GetComponent<MessageCollectibleEntity>();
        entity.Init(msg);
    }
}
