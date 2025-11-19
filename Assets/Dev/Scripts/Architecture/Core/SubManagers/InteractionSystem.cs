using System.Collections.Generic;
using UnityEngine;

public class InteractionSystem : MonoBehaviour
{
    private Dictionary<Vector2Int, IListener> listeners = new();
    private Dictionary<int, ITriggerable> triggerables = new();

    private Dictionary<IListener, ITriggerable> triggerLinks = new();

    [Header("Buffers")]
    [SerializeField]private List<InteractionBuffer> interactions = new List<InteractionBuffer>();




    public void RegisterListener(Vector2Int _gridCoord, IListener _listener)
    {
        listeners[_gridCoord] = _listener;
    }

    public void RegisterTriggerable(int _triggerableKey, ITriggerable _triggerable)
    {
        triggerables[_triggerableKey] = (_triggerable);
    }

    public void RegisterTriggerLink(int triggerKey, IListener _listener)
    {
        ITriggerable temp = null;

        if (triggerables.TryGetValue(triggerKey, out temp))
        {
            triggerLinks[_listener] = temp;
        }
        else
        {
            Debug.LogError($"Link by {_listener.ToString()} with key {triggerKey} points to nothing");
        }

    }

    public void SendEventToTriggerable(IListener _listener)
    {
        if (triggerLinks.TryGetValue(_listener, out ITriggerable _target))
        {
            _target.Trigger();
        }
        else
        {
            Debug.Log($"{_listener} fired event at nothing - no triggerlink registred");
        }
    }

    public void ActorMoved(Vector2Int _eventPos, IActor _actor, ActorInteractionType _interactionType)
    {
        TryTriggerInteractAt(_eventPos, _actor, _interactionType);
        CheckForBufferedEvents(_eventPos, _actor, _interactionType);
    }

    private void TryTriggerInteractAt(Vector2Int _eventPos, IActor _actor, ActorInteractionType interactionType)
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
        if (interactionType != ActorInteractionType.OnMove) return;
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


}
