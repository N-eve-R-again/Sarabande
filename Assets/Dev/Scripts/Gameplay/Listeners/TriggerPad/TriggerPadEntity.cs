using Sarabande.Core;
using Sarabande.Levels;
using UnityEngine;

public class TriggerPadEntity : MonoBehaviour, IListenerWithCallback, IResettable
{
    public TriggerPadConfig config;
    [SerializeField] private TriggerPadVisual visual;
    public bool needsCallback => !config.oneShot && config.timeToRearm < 0;

    public bool armed = true;

    public bool rearming = false;
    public bool occuped = false;
    public bool callbackRearmed = false;

    [SerializeField] private float timerTillRearm;

    [SerializeField] private ListenerInteractionLayer interactsWith;
    ListenerInteractionLayer IListener.interactionLayer => interactsWith;

    bool IListenerWithCallback.wantsCallback => needsCallback;

    public void Init(TriggerPadConfig _config, string _name)
    {
        config = _config; //je recupere ma config

        ListenerCreationHelper.SetupListenerEntity(this, this, _config.cell, _name); //comportment de base de setup

        visual.InitVisual(); //initialisation du visuel

        RegistryEvents.NotifyTryTriggerLinkRegistry(config.triggerKey,this); //j'enregistre mon triggerLink
    }

    private void Update()
    {
        if (config.oneShot) return;

        if(callbackRearmed && !armed && !occuped)//si j'ai un callback de rearm en attente => j'attends de plus etre occuped
        {
            callbackRearmed = false; //reset
            Rearm();// je me rearme
            return;
        }

        if(!rearming || needsCallback) return; //si je me réarme automatiquement avec timer

        timerTillRearm -= Time.deltaTime; //j'update le timer

        if (timerTillRearm < 0.20f) //j'active le tremblement quand 0.2s du timer restant
        {
            visual.StartTremble();
        }

        if (timerTillRearm <= 0f) // mon timer est fini, je me rearme
        {
            if (occuped) //si je suis occupé cette frame -> je mets en pause le timer - Grace period
            {
                timerTillRearm = 0f;
            }
            else
            {
                Rearm();
            }
        }

    }

    public void OnExitInteract()
    {
        occuped = false;
    }

    public bool OnInteract(ActorInteractionType interactionType)//return true si j'ai il y a quelqu'un sur moi
    {
        if (interactionType != ActorInteractionType.OnMove) return false;

        occuped = true;

        if (armed)
        {
            armed = false;
            visual.PressAnim();

            if (!config.oneShot) StartAutoRearm();
            //sound
            LevelEntityEvents.NotifyListenerTryCallTrigger(this);
        }

        return true;

    }

    private void Rearm() //reset des valeur pour le rechargement
    {
        timerTillRearm = 0f;
        visual.ResetAnim();
        rearming = false;
        armed = true;
    }

    private void StartAutoRearm() //pour commencer le ream avec timer
    {
        timerTillRearm = config.timeToRearm;
        rearming = true;
    }

    public void OnCallback() //si je me rearme avec un callback
    {
        callbackRearmed = true; //prochaine frame ou je suis pas occupé je me rearme
        if (occuped) visual.StartTremble(); //si je suis occupé, je ne peux pas me rearmer de suite, donc je tremble
    }

    public void ResetToInitial()
    {
        throw new System.NotImplementedException();
    }


}
