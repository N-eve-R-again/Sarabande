using Sarabande.Listeners;
using Sarabande.Triggerables;
using System;
using System.ComponentModel;


public class LevelEntityEvents
{

    // Quand un Listener essaye d'activer un Triggerable
    public static event Action<IListener> OnListenerTryCallTrigger; // Event

    [Obsolete("Utilise this.TryCallTrigger() via l'extension IListener.")]
    public static void Raise_ListenerTryCallTrigger(IListener listener) // Fonction Call
    => OnListenerTryCallTrigger?.Invoke(listener);

    // Quand un Triggerable veut Callback
    public static event Action<ITriggerable> OnTriggerableCallback;

    [Obsolete("Utilise this.SendCallback() via l'extension ITriggerable.")]
    public static void Raise_TriggerableCallback(ITriggerable triggerable)
    => OnTriggerableCallback?.Invoke(triggerable);

    //Quand un Signaler veut envoyer son Signal
    public static event Action<string[]> OnSignalSent;

    [Obsolete("Utilise this.Signal() via l'extension ISignaler.")]
    public static void Raise_SendSignal(string[] signals)
    => OnSignalSent?.Invoke(signals);

}





