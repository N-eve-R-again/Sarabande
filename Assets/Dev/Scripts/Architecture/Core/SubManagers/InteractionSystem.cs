using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

public class InteractionSystem
{
    private Dictionary<Vector2Int, IListener> listeners = new();
    private Dictionary<string, ITriggerable> triggerables = new();

    private Dictionary<IListener, List<ITriggerable>> triggerLinks = new();
    private Dictionary<ITriggerable, IListenerWithCallback> callbackLinks = new();

    private Dictionary<Vector2Int,ISensorExtension> sensorExtensions = new();
    
    private List<InteractionBuffer> interactions = new List<InteractionBuffer>();

    public void SubscribeToEvents()
    {
        RegistryEvents.OnListenerRegistry += RegisterListener;
        RegistryEvents.OnTryTriggerLinkRegistry += RegisterTriggerLink;
        RegistryEvents.OnTriggerableRegistry += RegisterTriggerable;
        RegistryEvents.OnSensorRegistry += RegisterSensor;

        LevelEntityEvents.OnListenerTryCallTrigger += SendEventToTriggerable;
        LevelEntityEvents.OnTriggerableCallback += SendTriggerableCallback;

        LevelEntityEvents.OnSignalSent += SendSignal;

        ActorEvents.OnActorMove += ActorMoved;
    }

    public void UnSubscribeToEvents()
    {
        RegistryEvents.OnListenerRegistry -= RegisterListener;
        RegistryEvents.OnTryTriggerLinkRegistry -= RegisterTriggerLink;
        RegistryEvents.OnTriggerableRegistry -= RegisterTriggerable;
        RegistryEvents.OnSensorRegistry -= RegisterSensor;

        LevelEntityEvents.OnListenerTryCallTrigger -= SendEventToTriggerable;
        LevelEntityEvents.OnTriggerableCallback -= SendTriggerableCallback;

        LevelEntityEvents.OnSignalSent -= SendSignal;

        ActorEvents.OnActorMove -= ActorMoved;
    }

    private void OnDestroy()
    {
        UnSubscribeToEvents();
    }

    private void RegisterSensor(Vector2Int cell, ISensorExtension sensor) => sensorExtensions[cell] = sensor;

    private void RegisterListener(Vector2Int _gridCoord, IListener _listener) => listeners[_gridCoord] = _listener;

    private void RegisterTriggerable(string _triggerableKey, ITriggerable _triggerable) => triggerables[_triggerableKey] = (_triggerable);

    private void RegisterTriggerLink(string triggerKey, IListener _listener)
    {
        if (triggerables.TryGetValue(triggerKey, out ITriggerable triggerable))
        {
            if (!triggerLinks.ContainsKey(_listener))
            {
                triggerLinks[_listener] = new List<ITriggerable>();
            }

            triggerLinks[_listener].Add(triggerable);

            if (_listener is IListenerWithCallback cbListener && cbListener.wantsCallback)
            {
                callbackLinks[triggerable] = cbListener;
            }

        }
        else
        {
            Debug.LogError($"Link by {_listener.ToString()} with key {triggerKey} points to nothing");
        }
    }
    private void SendSignal(ISignalerEnxtension signaler)
    {
        foreach (var signal in signaler.triggerableKeys)
        {
            if(triggerables.TryGetValue(signal, out ITriggerable triggerable))
            {
                triggerable.Trigger();
            }
            else
            {
                Debug.Log($"Signaler {signaler} was unsuccessful with signal - {signal}");
            }
        }
    }


    private void SendEventToTriggerable(IListener _listener)
    {
        if (triggerLinks.TryGetValue(_listener, out List<ITriggerable> _targets))
        {
            foreach (var target in _targets)
            {
                target.Trigger();
            }
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

    private void ActorMoved(IActor _actor, ActorInteractionData _interaction)
    {

        TrySensors(_interaction.cell, _interaction);

        if(_interaction.interactionType != ActorInteractionType.OnLeave)// OnMove, OnIntent, OnBump
        {
            (IListener _listener, bool _createBuffer) = TryTriggerInteractAt(_interaction.cell, _interaction);//On essaye de faire une interaction

            if (_listener != null && _createBuffer)
            {
                //le listener veut continuer d'écouter pour son ExitInteract()
                interactions.Add(new InteractionBuffer(_listener, _actor, _interaction.cell));
            }

        }
        else //OnLeave
        {
            CheckForBufferedEvents(_interaction.cell, _actor);//On regarde si on a des ExitEvents à trigger
        }

    }

    private void TrySensors(Vector2Int _eventPos, ActorInteractionData data)
    {
        if (sensorExtensions.TryGetValue(_eventPos, out ISensorExtension sensor)) //est ce que j'ai un listener à _eventPos
        {
            if (!sensor.IsActive) return;
            if (!sensor.interactionLayer.CanInteractWith(data.actorType)) return;

            if(data.interactionType == ActorInteractionType.OnLeave)
            {
                sensor.OnExit(); return;
            }

            if (data.interactionType == ActorInteractionType.OnMove)
            {
                sensor.OnEnter(); return;
            }
        }
    }

    private (IListener,bool) TryTriggerInteractAt(Vector2Int _eventPos, ActorInteractionData _interaction)
    {
        if (listeners.TryGetValue(_eventPos, out IListener listener)) //est ce que j'ai un listener à _eventPos
        {
            if (!listener.interactionLayer.CanInteractWith(_interaction.actorType)) return (null, false); //si l'acteur est pas du bon type on ignore

            bool willCreateBuffer = listener.OnInteract(_interaction);
            //on fait la logique du listener
            return (listener, willCreateBuffer);
        }

        return (null, false);
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
