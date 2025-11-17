using Sarabande.Core;
using System;
using UnityEngine;

public interface IListener
{

    ListenerInteractionLayer interactionLayer { get;}
    Vector2Int gridCoord { get;}

    public void OnInteract(ActorInteractionType interactionType);
    public void OnExitInteract(ActorInteractionType interactionType);

    public static void RegisterListener(IListener listener)
    {
        LevelEntitiesManager.I.RegisterListener(listener);
    }

    public static void SendEventToTriggerable(IListener listener)
    {
        LevelEntitiesManager.I.SendEventToTriggerable(listener);
    }
    public static void RegisterLinkToTrigger(int _triggerKey, IListener listener)
    {
        LevelEntitiesManager.I.RegisterTriggerLink(listener,_triggerKey);
    }
}





