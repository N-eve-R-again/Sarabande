using Sarabande.Core;
using Sarabande.Levels;
using Sarabande.Messages;
using UnityEngine;
using LEM = LevelEntitiesManager;

public class MessageCollectibleEntity : MonoBehaviour, IListener
{

    [SerializeField] private Vector2Int gridPosition;
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
    Vector2Int IListener.gridCoord => gridPosition;
    


    public void Init(MessageConfig _specs, string _name)
    {
        messageSystem = LEM.I.GetMessageSystem();

        gameObject.name = _name;
        specs = _specs;
        gridPosition = (Vector2Int)specs.cell;
        transform.position = SetPosition(specs.cell);
        transform.localScale = SetSize();

        IListener.RegisterListener(this);
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

    private Vector3 SetPosition(GridCoord _coord)
    {
        float x = (_coord.x + 0.5f) * LevelGlobalSettings.cellSize;
        float z = (_coord.z + 0.5f) * LevelGlobalSettings.cellSize;
        return new Vector3(x, markerY, z);
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
