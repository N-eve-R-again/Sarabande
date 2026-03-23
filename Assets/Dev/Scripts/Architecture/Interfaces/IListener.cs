using Sarabande.Core;
using Sarabande.Levels;
using System;
using UnityEngine;

public interface IListener
{
    public ListenerData listenerData { get; }
    InteractionLayer interactionLayer { get;}
    public bool OnInteract(ActorInteractionData _interaction);
    public void OnExitInteract();
    public void Register() => RegistryEvents.NotifyListenerRegistry(this);

}

public interface IListenerWithCallback : IListener
{
    public bool wantsCallback { get; }
    public void OnCallback();
}



