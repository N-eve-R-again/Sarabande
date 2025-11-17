using System;
using UnityEngine;

[Serializable]
public class ListenerInteractionLayer
{
    [SerializeField] private bool NME;
    [SerializeField] private bool Hero;


    public ListenerInteractionLayer(bool hero, bool nme)
    {
        Hero = hero;
        NME = nme;
    }
    public bool CanInteractWith(IActor actor)
    {
        switch (actor.type)
        {
            case ActorType.None: return false;
            case ActorType.Hero: return Hero;
            case ActorType.NME: return NME;
        }
        return false;

    }
}
