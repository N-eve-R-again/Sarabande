using Sarabande.Core;
using Sarabande.Levels;
using UnityEngine;

public class TriggerPadEntity : MonoBehaviour, IListenerWithCallback, IResettable
{
    private enum PadState
    {
        Armed,
        Disarmed,
        WaitingRearm
    }

    [SerializeField] private ListenerInteractionLayer interactsWith;

    [SerializeField] private TriggerObjectConfig config;
    [SerializeField] private TriggerPadVisual visual;
    [SerializeField] private RearmParameter rearmParameter;
    public bool needsCallback => !rearmParameter.oneShot && rearmParameter.timeToRearm < 0;

    [Header("State")]
    [SerializeField] private PadState state = PadState.Armed;
    [SerializeField] private bool occuped = false;
    [SerializeField] private float timer;

    //References de l'interface
    ListenerInteractionLayer IListener.interactionLayer => interactsWith;
    bool IListenerWithCallback.wantsCallback => needsCallback;

    public void Init(TriggerObjectConfig _config, string _name)
    {
        config = _config; //je recupere ma config
        rearmParameter = config.rearmParameter;
        ListenerCreationHelper.SetupListenerEntity(this, this, _config.cell, _name); //comportment de base de setup

        visual.InitVisual(); //initialisation du visuel

        RegistryEvents.NotifyTryTriggerLinkRegistry(config.triggerKeys[0],this); //j'enregistre mon triggerLink
    }


    private void Update()
    {
        if (state != PadState.WaitingRearm) return;

        if (needsCallback)
        {
            TryCallbackRearm();
        }
        else
        {
            UpdateTimerRearm();
        }
    }
    private void TryCallbackRearm()
    {
        AttemptRearm();
    }

    private void AttemptRearm()
    {
        if (occuped)
        {
            visual.StartTremble();
            return;
        }

        Rearm();
    }

    private void UpdateTimerRearm()
    {
        if (timer > 0f)
        {
            timer -= Time.deltaTime;
            if (timer < 0.2f)
            {
                visual.StartTremble();
            }

            return;
        }

        AttemptRearm();
    }


    public void OnExitInteract() => occuped = false;

    public bool OnInteract(ActorInteractionData _interaction)//return true si j'ai il y a quelqu'un sur moi
    {
        if (_interaction.interactionType != ActorInteractionType.OnMove)
            return false;

        occuped = true;

        if (state == PadState.Armed)
            OnPressed();

        return true;

        
    }

    private void OnPressed()
    {
        state = PadState.Disarmed;
        visual.PressAnim();

        LevelEntityEvents.NotifyListenerTryCallTrigger(this);

        if (!rearmParameter.oneShot)
        {
            if (!needsCallback)
                StartTimerRearm();
        }
    }

    private void Rearm() //reset des valeur pour le rechargement
    {
        state = PadState.Armed;
        visual.ResetAnim();
        timer = 0f;
    }
    private void StartTimerRearm()
    {
        timer = rearmParameter.timeToRearm;
        state = PadState.WaitingRearm;
    }

    public void OnCallback() //si je me rearme avec un callback
    {
        state = PadState.WaitingRearm;  //prochaine frame ou je suis pas occupé je me rearme
    }

    public void ResetToInitial()
    {
        throw new System.NotImplementedException();
    }


}
