using Sarabande.Core;
using UnityEngine;

public enum ActorInteractionType
{ OnMove, OnIntent,  OnBump, OnLeave}

public enum ActorType 
{ None, NME, Hero }

public class ActorInteractionData
{
    public ActorType actorType;
    public ActorInteractionType interactionType;
    public CardinalDirection directionality;
    public Vector2Int cell;

    public ActorInteractionData(ActorType actorType, ActorInteractionType interactionType)
    {
        this.actorType = actorType; this.interactionType = interactionType;
    }

    public void UpdateInteraction(CardinalDirection directionality, Vector2Int cell)
    {
        this.directionality = directionality; this.cell = cell;
    }
}
