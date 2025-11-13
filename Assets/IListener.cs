using System;
using UnityEngine;

public interface IListener
{
    enum ListenerType{
        FakeWall,
        ArrowTrap,
        Message
    }

    ListenerType type { get;}
    GameObject GameObject { get; }
    public void OnInteract();
    public void OnExitInteract();
}

public interface IActor
{
    InteractionLayer InteractionLayer { get; }
}



