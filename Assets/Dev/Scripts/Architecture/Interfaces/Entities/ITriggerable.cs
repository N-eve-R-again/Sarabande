namespace Sarabande.Triggerables
{
    public interface ITriggerable
    {
        public TriggerableData triggerableData { get; }
        public void Trigger();

        public void Register()
        {
            RegistryEvents.NotifyTriggerableRegistry(this);
        }
    }

}
