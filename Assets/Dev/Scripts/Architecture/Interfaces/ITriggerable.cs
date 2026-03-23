using Sarabande.Levels;
using UnityEngine;


public interface ITriggerable
{
    public TriggerableData triggerableData { get; }
    public void Trigger();

    public void Register()
    {
        RegistryEvents.NotifyTriggerableRegistry(this);  
    }
}

public interface ISensorExtension
{
    bool occupied { get; }
    bool IsActive { get; }
    InteractionLayer interactionLayer { get; } // Ajouté

    void OnEnter();
    void OnExit();
    void Activate();
    void Deactivate();
}

public interface ISignalerExtension
{
    
}
