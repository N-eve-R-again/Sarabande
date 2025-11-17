using Sarabande.Messages;
using System;
using System.Collections.Generic;
using UnityEngine;

public class LevelEntitiesManager : MonoBehaviour, IClearable
{
    private Dictionary<Vector2Int, IListener> listeners = new();
    private Dictionary<int,ITriggerable> triggerables = new();

    private Dictionary<IListener, ITriggerable> triggerLinks = new();

    private static LevelEntitiesManager Instance;
    public static LevelEntitiesManager I => Instance;

    [Header("Buffers")]
    private List<InteractionBuffer> interactions = new List<InteractionBuffer>();

    [Header("DebugLists")]
    [SerializeField] private DictionaryInInspector listenerDico;
    [SerializeField] private DictionaryInInspector triggerableDico;
    [SerializeField] private DictionaryInInspector triggerLinksDico;

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


    public void RegisterListener(IListener _listener)
    {
        listeners[_listener.gridCoord] = _listener;
        listenerDico.UpdateDictionary(listeners);
    }

    public void RegisterTriggerable(ITriggerable _triggerable)
    {
        triggerables[_triggerable.triggerableKey] = (_triggerable);
        triggerableDico.UpdateDictionary(triggerables);
    }

    public void RegisterTriggerLink(IListener _listener,int triggerKey)
    {
        ITriggerable temp = null;

        if(triggerables.TryGetValue(triggerKey, out temp))
        {
            triggerLinks[_listener] = temp;
            triggerLinksDico.UpdateDictionary(triggerLinks);
        }
        else
        {
            Debug.LogError($"Link by {_listener.ToString()} with key {triggerKey} points to nothing");
        }

    }

    public void TryTriggerInteractAt(Vector2Int _eventPos, IActor _actor, ActorInteractionType interactionType)
    {
        if (listeners.TryGetValue(_eventPos, out IListener listener))
        {
            if (!listener.interactionLayer.CanInteractWith(_actor)) return;
            listener.OnInteract(interactionType);
            interactions.Add(new InteractionBuffer(listener, _actor, _eventPos));
        }
    }

    private void CheckForBufferedEvents(Vector2Int _eventPos, IActor _actor, ActorInteractionType interactionType)
    {
        if (interactions.Count == 0) return;

        interactions.RemoveAll(buffer =>  // Pour chaque buffer
        {
            // Si les conditions sont remplies :
            if (buffer.actor == _actor && _eventPos != buffer.interactionPosition)
            {
                buffer.listener.OnExitInteract(interactionType);  // Déclenche l'événement
                return true;  // buffer supprimé
            }
            return false;  // buffer gardé et ignoré
        });
    }

    public void SendEventToTriggerable(IListener _listener)
    {
        if(triggerLinks.TryGetValue(_listener, out ITriggerable _target))
        {
            _target.Trigger();
        }
        else
        {
            Debug.Log($"{_listener} fired event at nothing - no triggerlink registred");
        }
    }

    public void ActorMoveEvent(Vector2Int _eventPos, IActor _actor, ActorInteractionType _interactionType)
    {
        TryTriggerInteractAt(_eventPos, _actor, _interactionType);
        CheckForBufferedEvents(_eventPos, _actor, _interactionType);


    }

}


[Serializable]
public class DictionaryInInspector
{
    [SerializeField] private List<string> debugKeys = new();
    [SerializeField] private List<string> debugValues = new();

    public void UpdateDictionary<TKey,TValue>(Dictionary<TKey, TValue> dico)
    {
        debugKeys.Clear();
        debugValues.Clear();

        foreach (var element in dico)
        {
            debugKeys.Add(element.Key?.ToString() ?? "null");
            debugValues.Add(element.Value?.ToString() ?? "null");
        }
    }

}