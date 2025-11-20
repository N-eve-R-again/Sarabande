using Sarabande.Core;
using System;
using UnityEngine;

public interface IListener
{
    ListenerInteractionLayer interactionLayer { get;}
    public bool OnInteract(ActorInteractionType interactionType);
    public void OnExitInteract();
}

public interface IListenerWithCallback : IListener
{
    public bool wantsCallback { get; }
    public void OnCallback();
}



