using System;
using System.Numerics;
using UnityEngine;

public class LevelEntityEvents
{

    // Quand un listener essaye d'activer un triggerable
    public static event Action<IListener> OnListenerTryCallTrigger; // Event
    public static void NotifyListenerTryCallTrigger(IListener listener) // Fonction Call
    => OnListenerTryCallTrigger?.Invoke(listener);

    // Quand un Triggerable veut Callback
    public static event Action<ITriggerable> OnTriggerableCallback;
    public static void NotifyTriggerableCallback(ITriggerable triggerable)
    => OnTriggerableCallback?.Invoke(triggerable);

}

public static class RegistryEvents
{
    // Quand un listener est créé
    public static event Action<Vector2Int, IListener> OnListenerRegistry; // Event
    public static void NotifyListenerRegistry(Vector2Int cell, IListener listener) // Fonction Call
    => OnListenerRegistry?.Invoke(cell, listener);


    // Quand un triggerable est créé
    public static event Action<int, ITriggerable> OnTriggerableRegistry; // Event
    public static void NotifyTriggerableRegistry(int triggerableKey, ITriggerable triggerable) // Fonction Call
    => OnTriggerableRegistry?.Invoke(triggerableKey, triggerable);


    // Quand un listener veut créer un triggerlink
    public static event Action<int, IListener> OnTryTriggerLinkRegistry; // Event
    public static void NotifyTryTriggerLinkRegistry(int _triggerKey, IListener listener) // Fonction Call
    => OnTryTriggerLinkRegistry?.Invoke(_triggerKey, listener);
}

public static class ActorEvents
{
    //Quand un actor fait un move
    public static event Action<Vector2Int, IActor, ActorInteractionType> OnActorMove; // Event
    public static void NotifyActorMove(Vector2Int _eventPos, IActor _actor, ActorInteractionType _interactionType) // Fonction Call
        => OnActorMove?.Invoke(_eventPos, _actor, _interactionType);

}
