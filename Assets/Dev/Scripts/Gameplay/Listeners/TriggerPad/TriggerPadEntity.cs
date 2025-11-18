using Sarabande.Core;
using Sarabande.Levels;
using UnityEngine;

public class TriggerPadEntity : MonoBehaviour, IListener, IResettable
{
    public TriggerPadConfig config;

    public bool armed = true;

    [SerializeField] private ListenerInteractionLayer interactsWith;
    ListenerInteractionLayer IListener.interactionLayer => interactsWith;

    public void Init(TriggerPadConfig _config, string _name)
    {
        config = _config;
        ListenerCreationHelper.SetupListenerEntity(this, this, _config.cell, _name);

        transform.localScale = SetSize();

        LevelEntityEvents.NotifyTryTriggerLinkRegistry(config.triggerKey,this);
    }

    public Vector3 SetSize()
    {
        float scaleXZ = LevelGlobalSettings.cellSize;
        return new Vector3(scaleXZ,1, scaleXZ);
    }

    public void OnExitInteract(ActorInteractionType interactionType)
    {
        //throw new System.NotImplementedException();
    }

    public void OnInteract(ActorInteractionType interactionType)
    {
        if (interactionType != ActorInteractionType.OnMove) return;
        if (armed)
        {
            armed = false;

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
