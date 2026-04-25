using Sarabande.Core;
using UnityEngine;
namespace Sarabande.Actors
{
    public interface IActor
    {
        public ActorType type { get; }
    }

#pragma warning disable CS0618
    public static class ActorEvents
    {
        public static void ActorMove(this IActor _actor, ActorInteractionData _interaction)
            => ActorRuntimeEvents.NotifyActorMove(_actor, _interaction); 

        public static void RegisterActor(this IActor _actor)
        {
            
        }
    }
#pragma warning restore CS0618

}


