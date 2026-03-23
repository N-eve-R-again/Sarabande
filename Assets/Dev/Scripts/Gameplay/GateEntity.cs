using Sarabande.Core;
using Sarabande.Levels;
using UnityEngine;

public class GateEntity : MonoBehaviour, ITriggerable
{
    public string triggerableKey;
    [SerializeField] private GateConfig config;
    [SerializeField] private ObstacleData obstacleData;

    [SerializeField] private GateVisual visual;
    private float timer;
    private bool down = false;

    public TriggerableData triggerableData => config;

    public void Init(GateConfig _config, string _name)
    {
        config = _config;
        obstacleData = new ObstacleData(ObstacleData.ObstacleType.ThinWall, config.cell, config.direction);
        visual.InitVisual(config.direction);
        gameObject.name = _name;
        triggerableKey = config.triggerKey;
        transform.position = GridUtils.CenterXZ(config.cell);
        if (config.startopen) OpenDoor();

        NavigationEvents.NotifyDynamicObstacle(obstacleData);

    }

    private void OpenDoor()
    {
        visual.DownAnim();
        down = true;
        if (config.type == GateType.Timer)
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
        else if (config.type == GateType.Toggle)
        {

            CloseDoor();
        }

    }

    private void Update()
    {

        if (config.type != GateType.Timer) return;
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
