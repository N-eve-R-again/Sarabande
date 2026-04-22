using Sarabande.Core;

namespace Sarabande.Listeners
{

    public interface IListener
    {
        public ListenerData listenerData { get; }
        InteractionLayer interactionLayer { get;}
        public bool OnInteract(ActorInteractionData _interaction);
        public void OnExitInteract();
        public void Register() => RegistryEvents.NotifyListenerRegistry(this);

    }

    public class ListenerBufferSubscription
    {
        public ActorInteractionType interactionType;
        public CardinalDirection cardinalDirection;

    }

    public interface IListenerWithCallback : IListener
    {
        public bool wantsCallback { get; }
        public void OnCallback();
    }
}


