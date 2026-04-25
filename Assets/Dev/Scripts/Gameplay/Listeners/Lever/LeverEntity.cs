using Sarabande.Core;
using Sarabande.Listeners;
using UnityEngine;
using Sarabande.EntityExtensions;

public class LeverEntity : MonoBehaviour, IListenerWithCallback, IResettable
{
    private enum LeverState
    {
        Desactivated,
        Activated,
        WaitingRearm
    }
    [SerializeField] private LeverState state;

    [SerializeField] private InteractionLayer interactsWith;
    public TriggerObjectConfig config;
    public LeverVisual visual;
    private float timer;
    public bool needsCallback => config.rearmType == RearmType.CallBack;

    InteractionLayer IListener.interactionLayer => interactsWith;
    bool IListenerWithCallback.wantsCallback => needsCallback;

    public ListenerData listenerData => config;

    public void Sync(TriggerObjectConfig _config)
    {
        config = _config;
    }
    public void SyncVisual()
    {
        transform.position = GridUtils.CenterXZ(config.cell);
        visual.SetRotation(config.attachedTo);
    }
    public void Init()
    {
        visual.InitVisual(config.rearmType == RearmType.Instant);
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

        this.TryCallTrigger();

        if(config.rearmType == RearmType.Timer)
        {
            timer = config.timeToRearm;
            state = LeverState.WaitingRearm; 
        }

        if(config.rearmType == RearmType.Instant)
        {
            Rearm();
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
