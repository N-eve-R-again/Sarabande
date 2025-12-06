using Sarabande.Core;
using System;
using UnityEngine;

public interface IListener
{
    InteractionLayer interactionLayer { get;}
    public bool OnInteract(ActorInteractionData _interaction);
    public void OnExitInteract();
}

public interface IListenerWithCallback : IListener
{
    public bool wantsCallback { get; }
    public void OnCallback();
}



