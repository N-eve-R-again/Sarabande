using Sarabande.Core;
using Sarabande.Levels;
using UnityEngine;
using Sarabande.Triggerables;
using Sarabande.Obstacles;
using Sarabande.EntityExtensions;

public class GateEntity : MonoBehaviour, ITriggerable, IInitializable
{
    [SerializeField] private GateConfig config;
    [SerializeReference] private DynamicObstacle dynamicObstacle;

    [SerializeField] private GateVisual visual;
    private float timer;
    private bool down = false;

    public TriggerableData triggerableData => config;

    public void Sync(GateConfig _config, string _name)
    {
        config = _config;
    }

    public void SyncVisual()
    {
        transform.position = GridUtils.CenterXZ(config.cell);
        visual.InitVisual(config.direction);
    }
    public void Init()
    {

        dynamicObstacle = new DynamicObstacle( config.cell, ObstacleData.ObstacleType.ThinWall, config.direction,!config.startopen);

        if (config.startopen) OpenDoor();

        dynamicObstacle.RegisterDynamicObstacle();

    }

    private void OpenDoor()
    {
        visual.DownAnim();
        down = true;
        if (config.type == GateConfig.Type.Timer)
        {
            timer = config.timer;
        }
        dynamicObstacle.SetActivated(false);
    }

    private void CloseDoor()
    {
        visual.UpAnim();

        this.SendTriggerableCallback();
        down = false;

        dynamicObstacle.SetActivated(true);
        dynamicObstacle.ChangeCell(dynamicObstacle.Cell + GridUtils.DirToVec2(CardinalDirection.East));
    }

    public void Trigger()
    {
        
        if (!down)
        {
            OpenDoor();

        }
        else if (config.type == GateConfig.Type.Toggle)
        {

            CloseDoor();
        }

    }

    private void Update()
    {

        if (config.type != GateConfig.Type.Timer) return;
        if (down)
        {
            timer -= Time.deltaTime;
            if (timer < 0)
            {
                timer = 0;
                CloseDoor();
            }
        }


    }


}
