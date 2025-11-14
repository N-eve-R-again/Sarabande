using Sarabande.Core;
using Sarabande.Levels;
using Sarabande.Messages;
using System;
using System.Collections.Generic;
using UnityEngine;

public class LevelEntitiesManager : MonoBehaviour, IClearable
{
    [Header("Listeners")]
    private Dictionary<Vector2Int, IListener> listeners = new();

    private static LevelEntitiesManager Instance;
    public static LevelEntitiesManager I => Instance;

    [Header("Buffers")]
    private List<InteractionBuffer> interactions = new List<InteractionBuffer>();

    [Header("DebugLists")]
    [SerializeField] private DebugListenerDico serializedDico;

    [Header("SubManagers")]
    [SerializeField] private MessageSystem messageSystem;

    public void Ready()
    {
        if(messageSystem == null) throw new MissingReferenceException("MessageSystem");
        Instance = this;

    }

    public void ClearObject()
    {
        //reset tout les objects ici
    }

    public MessageSystem GetMessageSystem() { return messageSystem; }


    public void RegisterListener(Vector2Int pos, IListener listener)
    {
        listeners[pos] = listener;
        serializedDico.UpdateDebugLists(listeners);
    }

    public void TryTriggerInteractAt(Vector2Int pos, IActor _actor)
    {
        if (listeners.TryGetValue(pos, out IListener listener))
        {
            if (!_actor.InteractionLayer.CanInteractWith(listener)) return;
            listener.OnInteract();
            interactions.Add(new InteractionBuffer(listener, _actor, pos));
        }
    }

    public void ActorMoveEvent(Vector2Int _EventPos, IActor _actor)
    {
        TryTriggerInteractAt(_EventPos, _actor);

        if (interactions.Count == 0) return;

        InteractionBuffer bufferToDelete = null;
        foreach (InteractionBuffer buffer in interactions)
        {
            if (_actor != buffer.actor) continue;
            if(_EventPos != buffer.interactionPosition)
            {
                buffer.listener.OnExitInteract();
                bufferToDelete = buffer;
                break;
            }
        }

        if (bufferToDelete != null) { interactions.Remove(bufferToDelete); }
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