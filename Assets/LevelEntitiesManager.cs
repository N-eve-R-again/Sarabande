using System;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;

public class LevelEntitiesManager : MonoBehaviour
{

    [Header("Listeners")]
    private Dictionary<Vector2Int, IListener> listeners = new();

    [SerializeField] public static LevelEntitiesManager Instance;

    [Header("DebugLists")]
    [SerializeField] private DebugListenerDico serializedDico;

    private void Awake()
    {
        Instance = this;
    }

    public void GlobalReset()
    {
        //reset tout les objects ici
    }


    public void RegisterListener(Vector2Int pos, IListener listener)
    {
        listeners[pos] = listener;
        serializedDico.UpdateDebugLists(listeners);
    }

    public void TryTriggerInteractAt(Vector2Int pos)
    {
        if (listeners.TryGetValue(pos, out IListener listener))
        {
            listener.OnInteract();
        }
    }

    public void ActorMoveEvent(Vector2Int _EventPos, IActor _actor)
    {
        TryTriggerInteractAt(_EventPos);
    }

}

[Serializable]
public class DebugListenerDico
{
    [SerializeField] private List<Vector2Int> debugKeys = new();
    [SerializeField] private List<GameObject> debugValues = new();

    public void UpdateDebugLists(Dictionary<Vector2Int, IListener> dico)
    {
        debugKeys.Clear();
        debugValues.Clear();

        foreach (var kvp in dico)
        {
            debugKeys.Add(kvp.Key);
            debugValues.Add(kvp.Value.GameObject);
        }
    }
}