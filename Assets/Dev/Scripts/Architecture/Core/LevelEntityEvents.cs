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
    public static event Action<string, ITriggerable> OnTriggerableRegistry; // Event
    public static void NotifyTriggerableRegistry(string triggerableKey, ITriggerable triggerable) // Fonction Call
    => OnTriggerableRegistry?.Invoke(triggerableKey, triggerable);


    // Quand un listener veut créer un triggerlink
    public static event Action<string, IListener> OnTryTriggerLinkRegistry; // Event
    public static void NotifyTryTriggerLinkRegistry(string _triggerKey, IListener listener) // Fonction Call
    => OnTryTriggerLinkRegistry?.Invoke(_triggerKey, listener);
}

public static class ActorEvents
{
    //Quand un actor fait un move
    public static event Action<IActor, ActorInteractionData> OnActorMove; // Event
    public static void NotifyActorMove(IActor _actor, ActorInteractionData _interaction) // Fonction Call
        => OnActorMove?.Invoke(_actor, _interaction);

}
