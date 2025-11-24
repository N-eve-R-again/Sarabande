using Sarabande.Core;
using Sarabande.Levels;
using UnityEngine;

public class ArrowTrapEntity : MonoBehaviour, ITriggerable
{
    public string triggerableKey;
    [SerializeField] private ArrowTrapConfig config;
    [SerializeField] private bool armed = true;
    [SerializeField] private ArrowTrapVisual visual;
    public void Init(ArrowTrapConfig _config, string _name)
    {
        config = _config;
        visual.InitVisual(config.travelDir);
        gameObject.name = _name;
        triggerableKey = config.triggerKey;
        transform.position = SetPosition(config.cell);

        RegistryEvents.NotifyTriggerableRegistry(triggerableKey, this);

    }
    private float timer = 0;
    private void Update()
    {
        if (!armed)
        {
            timer += Time.deltaTime;
            if (timer > 1f)
            {
                armed = true;
                timer = 0f;
                visual.SetArmed(armed);
                LevelEntityEvents.NotifyTriggerableCallback(this);
            }
        }
    }
    private Vector3 SetPosition(GridCoord c)
    {
        float x = (c.x + 0.5f) * LevelGlobalSettings.cellSize;
        float z = (c.z + 0.5f) * LevelGlobalSettings.cellSize;
        float y = 0.5f * LevelGlobalSettings.cellSize;
        return new Vector3(x, y, z);
    }

    public void Trigger()
    {
        if (armed) {
            armed = false;
            //Debug.Log($"ARROW SPAWNED BY {gameObject.name}");
            visual.SetArmed(armed);
        }
    }
}
