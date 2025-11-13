using System;
using UnityEngine;

[Serializable]
public class InteractionLayer
{
    [SerializeField] private bool FakeWall;
    [SerializeField] private bool ArrowTrap;

    public InteractionLayer(bool fakeWall, bool arrowTrap)
    {
        FakeWall = fakeWall;
        ArrowTrap = arrowTrap;
    }
    public bool CanInteractWith(IListener target)
    {
        switch (target.type)
        {
            case IListener.ListenerType.FakeWall: return FakeWall;
            case IListener.ListenerType.ArrowTrap: return ArrowTrap;
        }
        return false;

    }
}
