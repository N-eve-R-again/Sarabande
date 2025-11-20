using UnityEngine;

public interface IActor
{
    public ActorType type { get; }
}

public enum ActorInteractionType
{
    OnMove,
    OnIntent,
    OnBump,
    OnLeave
}

public enum ActorType
{
    None,
    NME,
    Hero
}