using Sarabande.Core;
using Sarabande.Levels;
using UnityEngine;

public class LeverEntity : MonoBehaviour, IListener, IResettable
{
    [SerializeField] private ListenerInteractionLayer interactsWith;
    public TriggerObjectConfig config;
    public ListenerInteractionLayer interactionLayer => interactsWith;


    public void Init(TriggerObjectConfig _config, string _name)
    {
        config = _config;
        ListenerCreationHelper.SetupListenerEntity(this, this, _config.cell, _name);

        //transform.localScale = SetSize();

        RegistryEvents.NotifyTryTriggerLinkRegistry(config.triggerKeys[0], this);
    }

    public bool OnInteract(ActorInteractionData _interaction)
    {
        throw new System.NotImplementedException();
    }

    public void OnExitInteract()
    {
        throw new System.NotImplementedException();
    }

    public void ResetToInitial()
    {
        throw new System.NotImplementedException();
    }
}
