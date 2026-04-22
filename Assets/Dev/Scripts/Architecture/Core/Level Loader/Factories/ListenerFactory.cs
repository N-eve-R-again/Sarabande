using Sarabande.Core;
using Sarabande.Levels;
using Sarabande.Listeners;

using UnityEngine;

public class ListenerFactory : MonoBehaviour, IClearable
{
    [Header("GameObject Folders")]
    private Transform fakeWallsParent;
    private Transform messagesParent;
    private Transform pressurePadsParent;
    private Transform leversParent;
    private Transform listenerFolder;

    private int listenerCount;
    private Transform ParentOfAll;

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

    public int BuildListeners(LevelData _levelData, Transform parent)
    {
        listenerCount = 0;

        ParentOfAll = parent;
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
            listenerCount++;
        }

        //tiles
        jobDone = true;
        return listenerCount;

    }

    private void CreateTriggerObject(TriggerObjectConfig config)
    {

        switch (config.type)
        {
            case TriggerObjectConfig.Type.InvisibleTrigger:
                break;

            case TriggerObjectConfig.Type.TriggerPad:
                GameObject triggerpad = Instantiate(pressurePadPrefab, pressurePadsParent);

                triggerpad.GetComponent<TriggerPadEntity>().Sync(config);
                break;

            case TriggerObjectConfig.Type.Lever:
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

        listenerFolder.SetParent(ParentOfAll, true);
    }

    private void CreateFakeWall(FakeWallData fk)
    {
        GameObject temp = Instantiate(fakeWallPrefab, fakeWallsParent);
        FakeWallEntity entity = temp.GetComponent<FakeWallEntity>();
        entity.Sync(fk);
        entity.SyncVisual();
    }

    private void CreateMessage(MessageConfig msg)
    {
        GameObject temp = Instantiate(messagePrefab, messagesParent);
        MessageCollectibleEntity entity = temp.GetComponent<MessageCollectibleEntity>();
        entity.Sync(msg);
        entity.SyncVisual();
    }
}
