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

    [SerializeField] private ListenerInteractionLayer interactsWith;
    ListenerInteractionLayer IListener.interactionLayer => interactsWith;


    public void Init(MessageConfig _specs, string _name)
    {
        specs = _specs;

        messageSystem = LEM.I.GetMessageSystem();

        ListenerCreationHelper.SetupListenerEntity(this,this,specs.cell, _name);

        SetPosition();
        transform.localScale = SetSize();
        messageIndex = messageSystem.Register(specs);

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

    public void OnExitInteract(ActorInteractionType interactionType)
    {
        
    }

    public void OnInteract(ActorInteractionType interactionType)
    {
        if (interactionType != ActorInteractionType.OnMove) return;

        if (collected) return;
        collected = true;
        Debug.Log("MESSAGE COLLECTED");
        messageSystem.Collect(messageIndex);

        //animation Collect
        transform.localScale = Vector3.zero;
    }


}
