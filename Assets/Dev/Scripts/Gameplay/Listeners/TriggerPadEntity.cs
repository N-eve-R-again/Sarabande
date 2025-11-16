using Sarabande.Core;
using Sarabande.Levels;
using UnityEngine;

public class TriggerPadEntity : MonoBehaviour, IListener, IResettable
{
    public TriggerPadConfig config;

    public bool armed = true;

    public Vector2Int gridPosition;

    public IListener.ListenerType type => IListener.ListenerType.PressurePad;
    Vector2Int IListener.gridCoord => gridPosition;

    public void Init(TriggerPadConfig _config, string _name)
    {
        config = _config;
        gameObject.name = _name;

        gridPosition = config.cell;
        transform.position = SetPosition(config.cell);
        transform.localScale = SetSize();

        
        IListener.RegisterListener(this);
        IListener.RegisterLinkToTrigger(config.triggerKey,this);
    }

    public Vector3 SetSize()
    {
        float scaleXZ = Mathf.Max(0.001f, LevelGlobalSettings.cellSize - 2f * 0.1f);
        return new Vector3(scaleXZ, 0.02f, scaleXZ);
    }
    private Vector3 SetPosition(GridCoord c)
    {
        float x = (c.x + 0.5f) * LevelGlobalSettings.cellSize;
        float z = (c.z + 0.5f) * LevelGlobalSettings.cellSize;
        return new Vector3(x, 0.02f * 0.5f, z);
    }

    public void OnExitInteract()
    {
        //throw new System.NotImplementedException();
    }

    public void OnInteract()
    {
        if (armed)
        {
            armed = false;

            //visuals,
            //sound
            IListener.SendEventToTriggerable(this);

        }

    }

    public void ResetToInitial()
    {
        throw new System.NotImplementedException();
    }


}
