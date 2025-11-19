using Sarabande.Core;
using Sarabande.Levels;
using UnityEngine;
using UnityEngine.Accessibility;

public class TriggerPadEntity : MonoBehaviour, IListener, IResettable
{
    public TriggerPadConfig config;
    [SerializeField] private TriggerPadVisual visual;
    public bool armed = true;
    public bool rearming = false;
    public bool occuped = false;
    public float timerTillRearm;

    [SerializeField] private ListenerInteractionLayer interactsWith;
    ListenerInteractionLayer IListener.interactionLayer => interactsWith;

    public void Init(TriggerPadConfig _config, string _name)
    {
        config = _config;
        ListenerCreationHelper.SetupListenerEntity(this, this, _config.cell, _name);

        transform.localScale = SetSize();

        LevelEntityEvents.NotifyTryTriggerLinkRegistry(config.triggerKey,this);
    }

    private void Update()
    {
        if(!rearming) return;

        if(timerTillRearm > 0f)
        {
            timerTillRearm -= Time.deltaTime;
            if(timerTillRearm < config.timeToRearm * 0.45f)
            {
                visual.StartTremble();
            }
            if (occuped) {
                timerTillRearm = config.timeToRearm;
                visual.StopTremble();
            }
        }

        if(timerTillRearm < 0f)
        {
            timerTillRearm = 0f;
            visual.ResetAnim();
            rearming = false;
            armed = true;
        }
    }

    public Vector3 SetSize()
    {
        float scaleXZ = LevelGlobalSettings.cellSize;
        return new Vector3(scaleXZ,1, scaleXZ);
    }

    public void OnExitInteract(ActorInteractionType interactionType)
    {

        if (interactionType != ActorInteractionType.OnMove) return;
        occuped = false;

        if (config.oneShot) return;
        if (!armed)
        {
            timerTillRearm = config.timeToRearm;
            rearming = true;
        }
        //throw new System.NotImplementedException();
    }

    public void OnInteract(ActorInteractionType interactionType)
    {
        if (interactionType != ActorInteractionType.OnMove) return;
        occuped = true;
        if (armed)
        {
            armed = false;
            visual.PressAnim();
            //visuals,
            //sound
            LevelEntityEvents.NotifyListenerTryCallTrigger(this);

        }


    }

    public void ResetToInitial()
    {
        throw new System.NotImplementedException();
    }


}
