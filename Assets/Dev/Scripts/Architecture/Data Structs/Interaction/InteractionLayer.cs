using System;
using UnityEngine;

[Serializable]
public class InteractionLayer
{
    [SerializeField] private bool NME;
    [SerializeField] private bool Hero;


    public InteractionLayer(bool hero, bool nme)
    {
        Hero = hero;
        NME = nme;
    }
    public bool CanInteractWith(ActorType _actorType)
    {
        switch (_actorType)
        {
            case ActorType.None: return false;
            case ActorType.Hero: return Hero;
            case ActorType.NME: return NME;
        }
        return false;

    }
}
