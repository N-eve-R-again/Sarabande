using Sarabande.Listeners;
using System;
using System.Collections.Generic;
using UnityEngine;

public static class PlaceBrushes
{


    public static List<ListenerData> listenerBrushes = new List<ListenerData>()
    {
        new FakeWallData(),
        new MessageConfig(),
        new TriggerObjectConfig(),
    };

}
