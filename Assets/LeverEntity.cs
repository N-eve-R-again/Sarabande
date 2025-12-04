using Sarabande.Core;
using Sarabande.Levels;
using Unity.VisualScripting;
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
    private float timer;
    public bool needsCallback => config.rearmType == RearmType.CallBack;

    ListenerInteractionLayer IListener.interactionLayer => interactsWith;
    bool IListenerWithCallback.wantsCallback => needsCallback;

    public void Init(TriggerObjectConfig _config)
    {
        config = _config;
        ListenerCreationHelper.SetupListenerEntity(this, this, config);
        visual.InitVisual(_config.attachedTo,config.rearmType == RearmType.CallBack);
        //transform.localScale = SetSize();

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

        if(config.rearmType == RearmType.Timer)
        {
            timer = config.timeToRearm;
            state = LeverState.WaitingRearm; 
        }

    }

    private void Update()
    {
        if (config.rearmType != RearmType.Timer) return;

        if (state != LeverState.WaitingRearm) return;
        {
            if(timer > 0f)
            {
                timer -= Time.deltaTime;
                return;
            }

            timer = 0f;
            Rearm();
        }
    }

    private void Rearm()
    {
        state = LeverState.Desactivated;
        visual.ResetAnim();
    }

    public void OnExitInteract() {}


    public void ResetToInitial()
    {
        throw new System.NotImplementedException();
    }

    public void OnCallback()
    {
        Rearm();
    }
}
