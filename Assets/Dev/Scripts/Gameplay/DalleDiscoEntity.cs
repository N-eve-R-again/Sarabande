using Sarabande.Core;
using UnityEngine;

public class DalleDiscoEntity : MonoBehaviour, ISensorExtension
{

    [SerializeField] private InteractionLayer interactsWith;
    [SerializeField] private DiscoSequenceEntity sequenceEntity;
    [SerializeField] private bool occupied;
    [SerializeField] private bool isActive;
    [SerializeField] private bool collected = false;
    [SerializeField] private MeshRenderer mr;
    public bool IsActive => isActive;
    bool ISensorExtension.occupied => occupied;

    public InteractionLayer interactionLayer => interactsWith;

    public void Init(Vector2Int cell, DiscoSequenceEntity _sequenceEntity)
    {
        sequenceEntity = _sequenceEntity;
        transform.position = GridUtils.CenterXZ(cell);
        RegistryEvents.NotifySensorRegistry(cell,this);
        mr.enabled = false;

    }

    private void Appel()
    {
        Debug.Log("MAMAN");
        sequenceEntity.Success(this);
    }

    public void Activate()
    {
        isActive = true;
        mr.enabled = true;
    }

    public void Deactivate()
    {
        isActive = false;
        mr.enabled = false;
        occupied = false;
    }

    public void OnEnter()
    {
        if (!collected)
        {
            Appel();
            collected = true;
        }

        occupied = true;
    }

    public void OnExit()
    {
        occupied = false;
    }
}
