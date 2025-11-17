using UnityEngine;

public interface IActor
{
    public ActorType type { get; }
}

public enum ActorInteractionType
{
    OnMove,
    OnIntent,
    OnCancelIntent,
    OnBump
}

public enum ActorType
{
    None,
    NME,
    Hero
}