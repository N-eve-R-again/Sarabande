using Sarabande.Core;
using Sarabande.Levels;
using UnityEngine;
using Sarabande.Listeners;
using Sarabande.EntityExtensions;

public class FakeWallEntity : MonoBehaviour, IListener, IResettable
{

    [SerializeField, Min(0f)] private float wallInset = 0.05f;
    [SerializeField, Min(0.1f)] private float wallHeight = 1f;

    [SerializeField] private bool revealed = false;
    [SerializeField] private float alphaOnRevealed = 0.5f;

    [SerializeField] private InteractionLayer interactsWith;
    public InteractionLayer interactionLayer => interactsWith;

    public ListenerData listenerData => wallData;
    public FakeWallData wallData;

    public void Sync(FakeWallData _config)
    {
        wallData = _config;

    }

    public void SyncVisual()
    {
        AbsoluteObjectNamer.GetName(wallData);
        SetSize(); //bientot dans le visual
        SetPosition();
    }

    public void SetSize()
    {

        float scaleXZ = Mathf.Max(0.001f, LevelGlobalSettings.cellSize - 2f * wallInset);
        transform.localScale = new Vector3(scaleXZ, wallHeight, scaleXZ);
    }
    private void SetPosition()
    {
        transform.position = GridUtils.CenterXZ(wallData.cell);
        transform.position += new Vector3(0, wallHeight * 0.5f, 0);
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

    public bool OnInteract(ActorInteractionData _interaction)
    {
        if (_interaction.interactionType != ActorInteractionType.OnIntent) return false;

        if (!revealed)
        {
            Discovered();
        }

        return true;
    }
    public void OnExitInteract() { }
}
