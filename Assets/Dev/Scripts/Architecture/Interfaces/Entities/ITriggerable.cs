namespace Sarabande.Triggerables
{
    public interface ITriggerable
    {
        public TriggerableData triggerableData { get; }
        public void Trigger();

    }

#pragma warning disable CS0618
    public static class TriggerableEvents
    {
        public static void RegisterTriggerable(this ITriggerable triggerable) => RegistryEvents.Raise_TriggerableRegistry(triggerable);
        public static void SendTriggerableCallback(this ITriggerable triggerable) => LevelEntityEvents.Raise_TriggerableCallback(triggerable);
    }
#pragma warning restore CS0618
}
