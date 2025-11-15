using Sarabande.Core;
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

    public static void Register(Vector2Int gridPosition,IListener listener)
    {
        LevelEntitiesManager.I.RegisterListener(gridPosition, listener);
    }
}

public interface IActor
{
    ListenerInteractionLayer InteractionLayer { get; }
}



