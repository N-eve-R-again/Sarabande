using Sarabande;
using Sarabande.Core;
using UnityEngine;

public class PortalEntity : MonoBehaviour
{

    [SerializeField] private ExitConfig portalConfig;

    public InteractionLayer interactionLayer => _interactionLayer;
    [SerializeField] private InteractionLayer _interactionLayer = new InteractionLayer(true, false);

    public bool armed = false;

    public void Init(ExitConfig exit)
    {

    }

    public void OnExitInteract()
    {


    }

    public bool OnInteract(ActorInteractionData _interaction)
    {
        return false;
    }

}
