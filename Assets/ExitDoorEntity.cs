using Sarabande.Core;
using UnityEngine;

public class ExitDoorEntity : MonoBehaviour, ITriggerable
{
    [SerializeField] private Vector2Int cell;
    [SerializeField] private CardinalDirection direction;
    [SerializeField] private Transform pivot;
    private int frameSkip;
    public Animator animator;
    private bool opened = false;
    private bool activated = false;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public void Init(EdgeExit config)
    {
        cell = config.fromCell;
        direction = config.direction;
        transform.position = GridUtils.CenterXZ(cell);
        SetRotation();
        frameSkip = 0;

        if (!config.startObstructed)
        {
            animator.SetTrigger("Open");
            opened = true;
        }
        else
        {
            opened = false;
        }
        RegistryEvents.NotifyTriggerableRegistry("exit", this);
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

    private void Update()
    {
        //if (!activated) return;
         if(frameSkip < 120)
        {
            frameSkip++;
            return;
        }
        else
        {
            frameSkip = 0;
            bool obstructed = NavigationEvents.QueryExitPortal(cell, cell + GridUtils.DirToVec2(direction), direction);
            //fallback si la porte devient obstruée
            if (obstructed)
            {
                if (opened)
                {
                    animator.SetTrigger("Close");
                    opened = false;
                }

            }

        }
    }

    public void Trigger()
    {
        if (!opened)
        {
            animator.SetTrigger("Open");
            opened = true;
        }

    }
}
