using Sarabande.Actors;
using System;


public static class ActorRuntimeEvents
{
    //Quand un actor fait un move
    public static event Action<IActor, ActorInteractionData> OnActorMove; // Event

    [Obsolete("Utilise this.ActorMove() via l'extension IActor.")]
    public static void NotifyActorMove(IActor _actor, ActorInteractionData _interaction) // Fonction Call
        => OnActorMove?.Invoke(_actor, _interaction);

}