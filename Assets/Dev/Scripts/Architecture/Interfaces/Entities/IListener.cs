using Sarabande.Core;

namespace Sarabande.Listeners
{

    public interface IListener
    {
        public ListenerData listenerData { get; }
        InteractionLayer interactionLayer { get;}
        public bool OnInteract(ActorInteractionData _interaction);
        public void OnExitInteract();

    }

    public interface IListenerWithCallback : IListener
    {
        public bool wantsCallback { get; }
        public void OnCallback();
    }

#pragma warning disable CS0618
    public static class ListenerEvents
    {
        public static void TryCallTrigger(this IListener listener) => LevelEntityEvents.Raise_ListenerTryCallTrigger(listener);

        public static void RegisterListener(this IListener listener) => RegistryEvents.Raise_ListenerRegistry(listener);
    }
#pragma warning restore CS0618

}


