using System.Collections.Generic;
using UnityEngine;

public class InteractionSystem : MonoBehaviour
{
    private Dictionary<Vector2Int, IListener> listeners = new();
    private Dictionary<int, ITriggerable> triggerables = new();

    private Dictionary<IListener, ITriggerable> triggerLinks = new();
    private Dictionary<ITriggerable, IListenerWithCallback> callbackLinks = new();

    [Header("Buffers")]
    [SerializeField]private List<InteractionBuffer> interactions = new List<InteractionBuffer>();

    public void SubscribeToEvents()
    {
        RegistryEvents.OnListenerRegistry += RegisterListener;
        RegistryEvents.OnTryTriggerLinkRegistry += RegisterTriggerLink;
        RegistryEvents.OnTriggerableRegistry += RegisterTriggerable;

        LevelEntityEvents.OnListenerTryCallTrigger += SendEventToTriggerable;
        LevelEntityEvents.OnTriggerableCallback += SendTriggerableCallback;

        ActorEvents.OnActorMove += ActorMoved;
    }

    public void UnSubscribeToEvents()
    {
        RegistryEvents.OnListenerRegistry -= RegisterListener;
        RegistryEvents.OnTryTriggerLinkRegistry -= RegisterTriggerLink;
        RegistryEvents.OnTriggerableRegistry -= RegisterTriggerable;

        LevelEntityEvents.OnListenerTryCallTrigger -= SendEventToTriggerable;
        LevelEntityEvents.OnTriggerableCallback -= SendTriggerableCallback;

        ActorEvents.OnActorMove -= ActorMoved;
    }

    private void RegisterListener(Vector2Int _gridCoord, IListener _listener) => listeners[_gridCoord] = _listener;

    private void RegisterTriggerable(int _triggerableKey, ITriggerable _triggerable) => triggerables[_triggerableKey] = (_triggerable);

    private void RegisterTriggerLink(int triggerKey, IListener _listener)
    {
        if (triggerables.TryGetValue(triggerKey, out ITriggerable triggerable))
        {
            triggerLinks[_listener] = triggerable;

            if(_listener is IListenerWithCallback cbListener && cbListener.wantsCallback)
            {
                callbackLinks[triggerable] = cbListener;
            }

        }
        else
        {
            Debug.LogError($"Link by {_listener.ToString()} with key {triggerKey} points to nothing");
        }
    }

    private void SendEventToTriggerable(IListener _listener)
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

    private void SendTriggerableCallback(ITriggerable triggerable)
    {
        if (callbackLinks.TryGetValue(triggerable, out IListenerWithCallback _target))
        {
            _target.OnCallback();
            Debug.Log($"{triggerable}'s callback was heard by {_target}");
        }
    }

    private void ActorMoved(Vector2Int _eventPos, IActor _actor, ActorInteractionType _interactionType)
    {
        /*switch (_interactionType)
        {
            case ActorInteractionType.OnMove: Debug.Log($"Interaction Move at {_eventPos}"); break;
            case ActorInteractionType.OnBump: Debug.Log($"Interaction Bump at {_eventPos}"); break;
            case ActorInteractionType.OnIntent: Debug.Log($"Interaction Intent at {_eventPos}");  break;
            case ActorInteractionType.OnLeave: Debug.Log($"Interaction Leave at {_eventPos}");  break;

        }
        */

        if(_interactionType != ActorInteractionType.OnLeave)// OnMove, OnIntent, OnBump
        {
            TryTriggerInteractAt(_eventPos, _actor, _interactionType); //On essaye de faire une interaction
        }
        else //OnLeave
        {
            CheckForBufferedEvents(_eventPos, _actor);//On regarde si on a des ExitEvents à trigger
        }

    }

    private void TryTriggerInteractAt(Vector2Int _eventPos, IActor _actor, ActorInteractionType interactionType)
    {
        if (listeners.TryGetValue(_eventPos, out IListener listener)) //est ce que j'ai un listener à _eventPos
        {
            if (!listener.interactionLayer.CanInteractWith(_actor)) return; //si l'acteur est pas du bon type on ignore

            //on fait la logique du listener
            bool willCreateBuffer = listener.OnInteract(interactionType);

            if (willCreateBuffer) //le listener veut continuer d'écouter pour son ExitInteract()
            { 
                interactions.Add(new InteractionBuffer(listener, _actor, _eventPos));
            }
        }
    }

    private void CheckForBufferedEvents(Vector2Int _eventPos, IActor _actor)
    {
        if (interactions.Count == 0) return; //aucune interaction à été buffered

        interactions.RemoveAll(buffer =>  // Pour chaque buffer
        {
            // Si les conditions sont remplies :
            if (buffer.actor == _actor && _eventPos == buffer.interactionPosition)
            {
                buffer.listener.OnExitInteract();  // Déclenche l'événement

                return true;  // buffer supprimé
            }
            return false;  // buffer gardé et ignoré
        });
    }


}
