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

        RegistryEvents.NotifyTriggerableRegistry(triggerableKey, this);

    }
    public void Trigger()
    {
        visual.DownAnim();
        if (config.type == GateType.Timer) {
            timer = config.timer;
            down = true;
        }


    }

    private void Update()
    {
        if (down)
        {
            timer -= Time.deltaTime;
            if (timer < 0)
            {
                timer = 0;
                down = false;
                LevelEntityEvents.NotifyTriggerableCallback(this);
                visual.UpAnim();
            }
        }


    }


}
