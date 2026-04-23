using Sarabande.Core;
using Sarabande.Levels;
using UnityEngine;
using Sarabande.Triggerables;
using Sarabande.EntityExtensions;

public class ExitDoorEntity : MonoBehaviour, IInitializable, ITriggerable, IPortalCreator
{
    [SerializeField] private ExitConfig exitConfig;
    [SerializeField] private Transform pivot;
    private int frameSkip;
    public Animator animator;
    [SerializeField] private bool opened = false;

    public TriggerableData triggerableData => data;
    [SerializeField] private DefaultTriggerableData data = new();
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public void Sync(ExitConfig config)
    {
        exitConfig = config;
        CreateLocalData();
    }

    public void SyncVisual()
    {
        transform.position = GridUtils.CenterXZ(exitConfig.cell);
        SetRotation();
    }

    private void CreateLocalData()
    {
        data.cell = exitConfig.cell;
        data.triggerKey = "exit";
    }

    public void Init()
    {


        frameSkip = 0;

        if (exitConfig.startState)
        {
            animator.SetTrigger("Open");
            opened = true;
        }
        else
        {
            opened = false;
        }

        this.RegisterPortal(exitConfig);

    }

    private void SetRotation()
    {
        float yRot = 0f;
        switch (exitConfig.direction)
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
            bool obstructed = !NavigationEvents.QueryExitPortal(exitConfig.cell, exitConfig.cell + GridUtils.DirToVec2(exitConfig.direction), exitConfig.direction);
            Debug.Log(obstructed);
            if(!obstructed) return; //on laisse open;

            //on referme
            animator.SetTrigger("Close");
            opened = false;

            this.ModifyPortal(exitConfig.cell, false);

            Debug.Log("obstructed, closed portal");


        }
    }

    public void Trigger()
    {
        if (!opened)
        {
            animator.SetTrigger("Open");
            opened = true;
            this.ModifyPortal(exitConfig.cell, true);
        }


    }
}
