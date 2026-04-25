using Sarabande.Core;
using Sarabande.EntityExtensions;
using Sarabande.Levels;
using Sarabande.Triggerables;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

public class ExitDoorEntity : MonoBehaviour, IInitializable, ITriggerable
{
    [SerializeField] private ExitDoorData exitDoorData;
    [SerializeField] private Portal portal;
    [SerializeField] private Transform pivot;
    private int frameSkip;
    public Animator animator;
    [SerializeField] private bool opened = false;

    public TriggerableData triggerableData => exitDoorData;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public void Sync(ExitDoorData config)
    {
        exitDoorData = config;

    }

    public void SyncVisual()
    {
        transform.position = GridUtils.CenterXZ(exitDoorData.cell);
        SetRotation();
    }

    public void Init()
    {
        portal = new Portal(exitDoorData.cell, exitDoorData.direction, exitDoorData.startState);

        frameSkip = 0;

        if (exitDoorData.startState)
        {
            animator.SetTrigger("Open");
            opened = true;
        }
        else
        {
            opened = false;
        }

        portal.RegisterPortal();

    }

    private void SetRotation()
    {
        float yRot = 0f;
        switch (exitDoorData.direction)
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
        if (!opened) return;

        //if (!activated) return;
        if (frameSkip < 120)
        {
            frameSkip++;
            return;
        }
        else
        {
            frameSkip = 0;
            return;
            /*bool obstructed = !NavigationEvents.QueryExitPortal(exitConfig.cell, exitConfig.cell + GridUtils.DirToVec2(exitConfig.direction), exitConfig.direction);
            Debug.Log(obstructed);
            if(!obstructed) return; //on laisse open;
            */
            //on referme
            animator.SetTrigger("Close");
            opened = false;


            portal.SetActivated(false);

            Debug.Log("obstructed, closed portal");


        }
    }

    public void Trigger()
    {
        if (!opened)
        {
            animator.SetTrigger("Open");
            opened = true;
            portal.SetActivated(true);
        }


    }
}
