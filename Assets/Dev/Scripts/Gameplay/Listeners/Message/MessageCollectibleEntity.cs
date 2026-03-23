using Sarabande.Core;
using Sarabande.Levels;
using Sarabande.Messages;
using UnityEngine;
using LEM = LevelEntitiesManager;

public class MessageCollectibleEntity : MonoBehaviour, IListener
{

    [SerializeField] private bool collected = false;
    [SerializeField] private int messageIndex = -1;
    [SerializeField] private MessageConfig specs;

    [SerializeField] private MessageSystem messageSystem;

    [Header("Marker Sprite (in-level)")]
    [SerializeField, Min(0f)] private float markerY = 0.02f;     // petit offset au-dessus du sol
    [SerializeField] private bool fitToCell = true;              // ajuste la largeur au cellSize
    [SerializeField, Range(0.1f, 2f)] private float spriteScale = 1f; // multiplicateur

    [SerializeField] private InteractionLayer interactsWith;
    InteractionLayer IListener.interactionLayer => interactsWith;

    public ListenerData listenerData => specs;

    public void Init(ListenerData _config)
    {
        specs = (MessageConfig)_config;

        ListenerCreationHelper.SetupListenerEntity(this,this, _config);

        SetPosition();
        transform.localScale = SetSize();
        messageIndex = UIEvents.NotifyRegisterMsgCollectible(specs);
        if (messageIndex == -1) Debug.Log("Something went wrong in message registry");

    }

    private Vector3 SetSize()
    {
        if (fitToCell)
        {
            float scale = LevelGlobalSettings.cellSize * spriteScale;
            return new Vector3(scale, scale, scale);
        }
        else return Vector3.one * spriteScale;

    }

    private void SetPosition()
    {
        transform.position += new Vector3(0, markerY, 0);
    }

    public void OnExitInteract()
    {
        
    }

    public bool OnInteract(ActorInteractionData _interaction)
    {
        if (messageIndex == -1) return false;
        if (_interaction.interactionType != ActorInteractionType.OnMove) return false;

        if (collected) return false;

        collected = true;
        Debug.Log("MESSAGE COLLECTED");
        UIEvents.NotifyCollectMsgCollectible(messageIndex);

        //animation Collect
        transform.localScale = Vector3.zero;

        return false;
    }


}
