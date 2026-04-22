using Sarabande.Core;
using Sarabande.Levels;
using UnityEngine;
using Sarabande.Triggerables;
using Sarabande.Obstacles;
using Sarabande.EntityExtensions;

public class GateEntity : MonoBehaviour, ITriggerable, IInitializable
{
    [SerializeField] private GateConfig config;
    [SerializeField] private ObstacleData obstacleData;

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

        obstacleData = new ObstacleData(ObstacleData.ObstacleType.ThinWall, config.cell, config.direction);

        if (config.startopen) OpenDoor();

        NavigationEvents.NotifyDynamicObstacle(obstacleData);

    }

    private void OpenDoor()
    {
        visual.DownAnim();
        down = true;
        if (config.type == GateConfig.Type.Timer)
        {
            timer = config.timer;
        }
        NavigationEvents.NotifyDynamicObstacleModification(config.cell, false);
    }

    private void CloseDoor()
    {
        visual.UpAnim();
        LevelEntityEvents.NotifyTriggerableCallback(this);
        down = false;
        NavigationEvents.NotifyDynamicObstacleModification(config.cell, true);
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
