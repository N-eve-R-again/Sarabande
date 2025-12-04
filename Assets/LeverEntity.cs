using Sarabande.Core;
using Sarabande.Levels;
using UnityEngine;

public class LeverEntity : MonoBehaviour, IListenerWithCallback, IResettable
{
    private enum LeverState
    {
        Desactivated,
        Activated,
        WaitingRearm
    }
    [SerializeField] private LeverState state;

    [SerializeField] private ListenerInteractionLayer interactsWith;
    public TriggerObjectConfig config;
    public LeverVisual visual;
    public bool needsCallback => true;

    ListenerInteractionLayer IListener.interactionLayer => interactsWith;
    bool IListenerWithCallback.wantsCallback => needsCallback;

    public void Init(TriggerObjectConfig _config, string _name)
    {
        config = _config;
        ListenerCreationHelper.SetupListenerEntity(this, this, _config.cell, _name);
        visual.InitVisual(_config.attachedTo,config.rearmType == RearmType.CallBack);
        //transform.localScale = SetSize();

        RegistryEvents.NotifyTryTriggerLinkRegistry(config.triggerKeys[0], this);
    }


    public bool OnInteract(ActorInteractionData _interaction)
    {
        if(_interaction.interactionType != ActorInteractionType.OnBump) return false;
        if(config.attachedTo != GridUtils.Opposite(_interaction.directionality)) return false;
        if (state == LeverState.Desactivated)
            OnPressed();


        return true;
    }

    private void OnPressed()
    {
        state = LeverState.Activated;
        visual.PressAnim();
        LevelEntityEvents.NotifyListenerTryCallTrigger(this);

    }

    private void Rearm()
    {
        state = LeverState.Desactivated;
        visual.ResetAnim();
    }



    public void OnExitInteract()
    {
        
    }

    public void ResetToInitial()
    {
        throw new System.NotImplementedException();
    }

    public void OnCallback()
    {
        Rearm();
    }
}
