using Sarabande.EntityExtensions;
using Sarabande.Listeners;
using Sarabande.Triggerables;
using System;
using UnityEngine;

public static class RegistryEvents
{
    // Quand un listener est créé
    public static event Action<IListener> OnListenerRegistry; // Event
    public static void Raise_ListenerRegistry(IListener listener) // Fonction Call
    => OnListenerRegistry?.Invoke(listener);

    // Quand un sensor est crée
    public static event Action<ISensor> OnSensorRegistry; // Event
    public static void Raise_SensorRegistry(ISensor sensor) // Fonction Call
    => OnSensorRegistry?.Invoke(sensor);

    // Quand un triggerable est créé
    public static event Action<ITriggerable> OnTriggerableRegistry; // Event
    public static void Raise_TriggerableRegistry(ITriggerable triggerable) // Fonction Call
    => OnTriggerableRegistry?.Invoke(triggerable);

}