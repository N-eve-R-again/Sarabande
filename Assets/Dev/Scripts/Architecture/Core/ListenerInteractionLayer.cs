using System;
using UnityEngine;

[Serializable]
public class ListenerInteractionLayer
{
    [SerializeField] private bool FakeWall;
    [SerializeField] private bool ArrowTrap;
    [SerializeField] private bool Message;

    public ListenerInteractionLayer(bool fakeWall, bool arrowTrap, bool message)
    {
        FakeWall = fakeWall;
        ArrowTrap = arrowTrap;
        Message = message;
    }
    public bool CanInteractWith(IListener target)
    {
        switch (target.type)
        {
            case IListener.ListenerType.FakeWall: return FakeWall;
            case IListener.ListenerType.PressurePad: return ArrowTrap;
            case IListener.ListenerType.Message: return Message;
        }
        return false;

    }
}
