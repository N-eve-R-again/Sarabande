using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

using Sarabande.Actors;
using Sarabande.Triggerables;
using Sarabande.Listeners;
using Sarabande.EntityExtensions;


public class InteractionSystem
{
    private RegistryDatabase database;
    private List<InteractionBuffer> interactions = new List<InteractionBuffer>();

    public void SetDatabase(RegistryDatabase _database) => database = _database;

    public void SendSignal(string[] keys)
    {
        foreach (string signal in keys)
        {
            ITriggerable triggerable = database.GetTriggerableByKey(signal);
            if (triggerable != null) 
            {
                triggerable.Trigger();
                continue;
            }

            UnityEvent _event = database.GetGlobalEventByKey(signal);

            if (_event != null)
            {
                _event.Invoke();
                continue;
            }

            Debug.Log($"[Interaction System] Signaler was unsuccessful with signal - {signal}");

        }
    }


    public void SendEventToTriggerable(IListener _listener)
    {
        bool eventfired = false;

        List<ITriggerable> _targets = database.GetTriggerablesByLinks(_listener);

        if (_targets != null && _targets.Count > 0) 
        {
            foreach (var target in _targets)
            {
                target.Trigger();
            }
            eventfired = true;

        }

        List<UnityEvent> _events = database.GetGlobalEventsByLinks(_listener);

        if(_events!= null && _events.Count > 0)
        {
            foreach (var _event in _events)
            {
                _event.Invoke();
            }
            eventfired = true;
        }


        if (!eventfired) Debug.Log($"[Interaction System] {_listener} fired event at nothing - no triggerlink registred");
    }


    public void SendTriggerableCallback(ITriggerable triggerable)
    {
        List<IListenerWithCallback> _targets = database.GetCallbacksByLinks(triggerable);

        if (_targets != null && _targets.Count > 0)
        {
            foreach (var _target in _targets)
            {
                _target.OnCallback();
                Debug.Log($"[Interaction System] {triggerable}'s callback was heard by {_target}");
            }
        }
    }

    public void ActorMoved(IActor _actor, ActorInteractionData _interaction)
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
        ISensor sensor = database.GetSensorExtensionByCell(_eventPos);

        if (sensor != null) {
            if (!sensor.IsActive) return;
            if (!sensor.interactionLayer.CanInteractWith(data.actorType)) return;

            if (data.interactionType == ActorInteractionType.OnLeave)
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
        IListener listener = database.GetListenerByCell(_eventPos);
        if (listener != null) {
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
