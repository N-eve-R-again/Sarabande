using UnityEngine;


public interface ITriggerable
{
    public int triggerableKey { get;}
    public void Trigger();

    public static void RegisterTriggerable(ITriggerable triggerable)
    {
        LevelEntitiesManager.I.RegisterTriggerable(triggerable);
    }

}
