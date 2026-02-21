using Sarabande.Core;
using Sarabande.Levels;
using UnityEngine;

public class EnterDoorEntity : MonoBehaviour, ITriggerable
{
    [SerializeField] private Vector2Int cell;
    [SerializeField] private CardinalDirection direction;
    [SerializeField] private Transform pivot;
    public Animator animator;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public void Init(ActorSpawn config)
    {
        cell = config.spawnCell;
        direction = config.spawnDirection;
        transform.position = GridUtils.CenterXZ(cell);
        SetRotation();

        RegistryEvents.NotifyTriggerableRegistry("enter", this);
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
