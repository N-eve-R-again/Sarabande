using UnityEngine;


public interface ITriggerable
{
    public void Trigger();

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
