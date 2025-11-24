using Sarabande.Core;
using Sarabande.Levels;
using UnityEngine;

public class TestTriggerable : MonoBehaviour, ITriggerable
{
    public string triggerableKey;

    public void Init(GridCoord _gridCoord, string _key, string _name)
    {
        gameObject.name = _name;
        triggerableKey = _key;
        transform.position = SetPosition(_gridCoord);
        RegistryEvents.NotifyTriggerableRegistry(_key, this);

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
        Debug.Log($"{gameObject.name} CE TRIGGER FONCTIONNE C'EST FOU");
    }
}
