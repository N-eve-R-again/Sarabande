using Sarabande.Core;
using Sarabande.Levels;
using UnityEngine;

public class FakeWallEntity : MonoBehaviour, IListener, IResettable
{



    [SerializeField] private Vector2Int gridPosition;

    [SerializeField, Min(0f)] private float wallInset = 0.05f;
    [SerializeField, Min(0.1f)] private float wallHeight = 1f;

    [SerializeField] private bool revealed = false;
    [SerializeField] private float alphaOnRevealed = 0.5f;

    [SerializeField] private ListenerInteractionLayer interactsWith;
    ListenerInteractionLayer IListener.interactionLayer => interactsWith;
    Vector2Int IListener.gridCoord => gridPosition;

    public void Init(GridCoord _coord, string _name)
    {
        gameObject.name = _name;

        gridPosition = (Vector2Int)_coord;
        transform.position = SetPosition(_coord);
        transform.localScale = SetSize();

        IListener.RegisterListener(this);
    }

    public Vector3 SetSize()
    {
        float scaleXZ = Mathf.Max(0.001f, LevelGlobalSettings.cellSize - 2f * wallInset);
        return new Vector3(scaleXZ, wallHeight, scaleXZ);
    }
    private Vector3 SetPosition(GridCoord c)
    {
        float x = (c.x + 0.5f) * LevelGlobalSettings.cellSize;
        float z = (c.z + 0.5f) * LevelGlobalSettings.cellSize;
        return new Vector3(x, wallHeight * 0.5f, z);
    }

    private void Discovered()
    {
        gameObject.GetComponent<Renderer>().material.color = new Color(1, 1, 1, alphaOnRevealed);
        revealed = true;
    }
    private void UnDiscover()
    {
        gameObject.GetComponent<Renderer>().material.color = Color.white;
        revealed = false;
    }

    public void ResetToInitial()
    {
        UnDiscover(); 
    }

    public void OnInteract(ActorInteractionType interactionType)
    {
        if (interactionType != ActorInteractionType.OnIntent) return;
        if (!revealed)
        {
            Discovered();
        }
    }
    public void OnExitInteract(ActorInteractionType interactionType)
    {
        if (revealed)
        {
            //UnDiscover();
        }
    }
}
