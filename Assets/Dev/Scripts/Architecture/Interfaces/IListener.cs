using Sarabande.Core;
using System;
using UnityEngine;

public interface IListener
{
    enum ListenerType{
        FakeWall,
        PressurePad,
        Message
    }

    ListenerType type { get;}
    Vector2Int gridCoord { get;}

    public void OnInteract();
    public void OnExitInteract();

    public static void RegisterListener(IListener listener)
    {
        LevelEntitiesManager.I.RegisterListener(listener);
    }

    public static void RegisterLinkToTrigger(int _triggerKey, IListener listener)
    {
        LevelEntitiesManager.I.RegisterTriggerLink(listener,_triggerKey);
    }
}





