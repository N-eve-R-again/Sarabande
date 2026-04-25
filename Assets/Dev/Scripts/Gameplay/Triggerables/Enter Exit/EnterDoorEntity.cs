using Sarabande.Core;
using Sarabande.Actors;
using Sarabande.Triggerables;
using Sarabande.EntityExtensions;
using UnityEngine;

public class EnterDoorEntity : MonoBehaviour, ITriggerable
{
    [SerializeField] private HeroData actorSpawn;
    [SerializeField] private Vector2Int cell;
    [SerializeField] private CardinalDirection direction;
    [SerializeField] private Transform pivot;
    public Animator animator;

    public TriggerableData triggerableData => data;
    public DefaultTriggerableData data = new();
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public void Sync(HeroData config)
    {
        actorSpawn = config;
        direction = actorSpawn.spawnDirection;
        CreateLocalData();
    }

    private void CreateLocalData()
    {
        data = new();
        data.cell = actorSpawn.cell;
        data.triggerKey = "enter";
    }



    public void SyncVisual()
    {
        transform.position = GridUtils.CenterXZ(actorSpawn.cell);
        SetRotation();
    }

    private void SetRotation()
    {
        float yRot = 0f;
        switch (direction)
        {
            case CardinalDirection.South:
                yRot = 0f; break;
            case CardinalDirection.North: yRot = 180f; break;
            case CardinalDirection.West: yRot = 90f; break;
            case CardinalDirection.East: yRot = 270f; break;

        }
        pivot.rotation = Quaternion.Euler(0f, yRot, 0f);
    }

    public void Trigger()
    {
        throw new System.NotImplementedException();
    }
}
