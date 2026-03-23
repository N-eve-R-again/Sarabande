using System.Collections.Generic;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.Events;

public class EntityRegister
{
    public void ImportGlobalEvents(RegistryDatabase database, List<EventTriggerable> _globalEvents)
    {
        foreach (var item in _globalEvents)
        {
            database.SetGlobalEvent(item.triggerKey, item._event);
        }
    }

    public void RegisterSensor(RegistryDatabase database, Vector2Int cell, ISensorExtension sensor)
    => database.SetSensorExtension(cell,sensor);

    public void RegisterTriggerable(RegistryDatabase database, ITriggerable _triggerable)
    {
        Debug.Log("[EntityRegister] Triggerable " + _triggerable.ToString() + " registered");
        database.SetTriggerable(_triggerable.triggerableData.triggerKey, _triggerable);
    }

    public void RegisterListener(RegistryDatabase database, IListener _listener)
    {
        Debug.Log("[EntityRegister] Listener " + _listener.ToString() + " registered");
        Vector2Int gridCoord = _listener.listenerData.cell;
        string[] triggerKeys = _listener.listenerData.triggerKeys;

        database.SetListener(gridCoord,_listener);

        if (triggerKeys.Length > 0)
        {
            TryCreateTriggerLinks(database, triggerKeys, _listener);
        }
    }


    private void TryCreateTriggerLinks(RegistryDatabase database, string[] _triggerKeys, IListener _listener)
    {
        foreach (var triggerKey in _triggerKeys)
        {
            ITriggerable triggerable = database.GetTriggerableByKey(triggerKey);
            if(triggerable != null)
            {
                database.SetTriggerableLink(_listener, triggerable);

                if (_listener is IListenerWithCallback cbListener && cbListener.wantsCallback)
                {
                    database.SetCallbackLink(cbListener,triggerable);
                }

                continue;
            }

            UnityEvent _event = database.GetGlobalEventByKey(triggerKey);

            if (_event != null) {
                database.SetGlobalEventLink(_listener, _event);
                Debug.Log("[EntityRegister] Previous Listener Linked with a Global Event " + triggerKey);
                continue;
            }

            Debug.LogError($"[EntityRegister] Link by {_listener.ToString()} with key {triggerKey} points to nothing");
        }


    }
}
