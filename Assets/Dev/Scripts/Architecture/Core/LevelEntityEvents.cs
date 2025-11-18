using System;
using UnityEngine;

public class LevelEntityEvents
{

    // Quand un listener est créé
    public static event Action<Vector2Int,IListener> OnListenerRegistry;

    // Quand un triggerable est créé
    public static event Action<int,ITriggerable> OnTriggerableRegistry;

    // Quand un listener essaye d'activer un triggerable
    public static event Action<IListener> OnListenerTryCallTrigger;

    // Quand un listener veut créer un triggerlink
    public static event Action<int,IListener> OnTryTriggerLinkRegistry;

    public static void NotifyListenerRegistry(Vector2Int cell,IListener listener)
    {
        OnListenerRegistry?.Invoke(cell,listener);
    }

    public static void NotifyTriggerableRegistry(int triggerableKey, ITriggerable triggerable)
    {
        OnTriggerableRegistry?.Invoke(triggerableKey,triggerable);
    }

    public static void NotifyListenerTryCallTrigger(IListener listener)
    {
        OnListenerTryCallTrigger?.Invoke(listener);
    }
    public static void NotifyTryTriggerLinkRegistry(int _triggerKey, IListener listener)
    {
        OnTryTriggerLinkRegistry?.Invoke(_triggerKey, listener);
    }
}
