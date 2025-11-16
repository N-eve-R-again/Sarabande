using UnityEngine;


public interface ITriggerable
{
    GameObject GameObject { get; }

    public int triggerableKey { get;}
    public void Trigger();

    public static void RegisterTriggerable(ITriggerable triggerable)
    {
        LevelEntitiesManager.I.RegisterTriggerable(triggerable);
    }

}
