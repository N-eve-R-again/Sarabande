using Sarabande.Core;
using Sarabande.Levels;
using UnityEngine;
using static UnityEngine.Rendering.STP;

public class GateEntity : MonoBehaviour, ITriggerable
{
    public string triggerableKey;
    [SerializeField] private GateConfig config;

    [SerializeField] private GateVisual visual;
    private float timer;
    private bool down = false;
    public void Init(GateConfig _config, string _name)
    {
        config = _config;

        visual.InitVisual(config.direction);
        gameObject.name = _name;
        triggerableKey = config.triggerKey;
        transform.position = GridUtils.CenterXZ(config.cell);
        if (config.startopen) OpenDoor();
        RegistryEvents.NotifyTriggerableRegistry(triggerableKey, this);

    }

    private void OpenDoor()
    {
        visual.DownAnim();
        down = true;
        if (config.type == GateType.Timer)
        {
            timer = config.timer;
        }
    }

    private void CloseDoor()
    {
        visual.UpAnim();
        LevelEntityEvents.NotifyTriggerableCallback(this);
        down = false;
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
