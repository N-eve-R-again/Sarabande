using Sarabande.Core;
using System;
using UnityEngine;

public interface IListener
{
    ListenerInteractionLayer interactionLayer { get;}
    public void OnInteract(ActorInteractionType interactionType) { return; }
    public void OnExitInteract(ActorInteractionType interactionType) { return; }
}





