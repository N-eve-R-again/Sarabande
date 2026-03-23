using Sarabande.Core;
using Sarabande.Levels;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


public class BootStraper : MonoBehaviour
{
    public LevelContext context;
    public LevelRoot root;
    [SerializeField] private LevelLoader levelLoader;

    public List<IListener> listeners = new List<IListener>();
    public List<ITriggerable> triggerables = new List<ITriggerable>();
    public List<IActor> actors = new List<IActor>();

    private void Start()
    {
        Boot();
    }

    private void Boot()
    {
        root.Init();

        levelLoader = FindFirstObjectByType<LevelLoader>();
        if (levelLoader != null && levelLoader.gameObject.activeInHierarchy)
        {
            levelLoader.Construct(context.LevelData,root.transform);
            if (!levelLoader.ready) Debug.LogError("CONTRUCTING FAILED");
        }

        RegistryStep();

        root.BuildCollisionSets(context.LevelData);
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public void RegistryStep()
    {
        listeners.Clear();
        listeners = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).OfType<IListener>().ToList();

        triggerables.Clear();
        triggerables = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).OfType<ITriggerable>().ToList();

        actors.Clear();
        actors = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).OfType<IActor>().ToList();

        Debug.Log($"<color=cyan> Bootstrap Detection Log : </color>\n " +
            $"{listeners.Count} Listeners Detected " +
            $"{triggerables.Count} Triggerables Detected " +
            $"{actors.Count} Actors Detected ");

        foreach (var triggerable in triggerables)
        {
            triggerable.Register();
        }

        foreach (var listener in listeners)
        {
            listener.Register();
        }
        Debug.Log("<color=green> Finished Registry </color>");



    }

}
