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

    public void TryTriggerInteractAt(Vector2Int pos, IActor _actor)
    {
        if (listeners.TryGetValue(pos, out IListener listener))
        {
            if (!_actor.InteractionLayer.CanInteractWith(listener)) return;
            listener.OnInteract();
            interactions.Add(new InteractionBuffer(listener, _actor, pos));
        }
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

    public void ActorMoveEvent(Vector2Int _EventPos, IActor _actor)
    {
        TryTriggerInteractAt(_EventPos, _actor);

        if (interactions.Count == 0) return;

        interactions.RemoveAll(buffer =>  // Pour chaque buffer
        {
            // Si les conditions sont remplies :
            if (buffer.actor == _actor && _EventPos != buffer.interactionPosition)
            {
                buffer.listener.OnExitInteract();  // Déclenche l'événement
                return true;  // buffer supprimé
            }
            return false;  // buffer gardé et ignoré
        });

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