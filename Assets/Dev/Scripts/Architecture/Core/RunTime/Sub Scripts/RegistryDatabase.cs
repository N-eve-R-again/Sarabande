using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

using Sarabande.Listeners;
using Sarabande.Triggerables;
using Sarabande.EntityExtensions;


public class RegistryDatabase
{
    //Direct References
    private Dictionary<string, UnityEvent> globalEvents = new();//get
    private Dictionary<Vector2Int, IListener> listeners = new();//get
    private Dictionary<string, ITriggerable> triggerables = new();//get

    private Dictionary<IListener, List<ITriggerable>> triggerLinks = new();
    private Dictionary<ITriggerable, List<IListenerWithCallback>> callbackLinks = new();
    private Dictionary<IListener, List<UnityEvent>> globalEventsLinks = new();//get

    private Dictionary<Vector2Int, ISensor> sensorExtensions = new();


    //GLOBAL EVENTS GET SET
    public UnityEvent GetGlobalEventByKey(string key)
    {
        if (globalEvents.TryGetValue(key, out UnityEvent _event)) return _event;
        return null;
    }
    public void SetGlobalEvent(string key, UnityEvent globalEvent) => globalEvents[key] = globalEvent;





    //SENSORS GET SET
    public ISensor GetSensorExtensionByCell(Vector2Int cell)
    {
        if (sensorExtensions.TryGetValue(cell, out ISensor sensor)) return sensor;
        return null;
    }

    public void SetSensorExtension(Vector2Int cell, ISensor sensor) => sensorExtensions[cell] = sensor;

    //LISTENERS GET SET
    public IListener GetListenerByCell(Vector2Int cell)
    {
        if(listeners.TryGetValue(cell,out IListener listener)) return listener;
        return null;
    }
    public void SetListener(Vector2Int cell, IListener listener) 
        => listeners[cell] = listener;





    //TRIGGERABLE GET SET
    public ITriggerable GetTriggerableByKey(string key)
    {
        if (triggerables.TryGetValue(key, out ITriggerable triggerable)) return triggerable;
        return null;
    }

    public void SetTriggerable(string key, ITriggerable triggerable) 
        => triggerables.Add(key, triggerable);




    //Get Global Events Links 
    public List<UnityEvent> GetGlobalEventsByLinks(IListener listener)
    {
        if (globalEventsLinks.TryGetValue(listener, out List<UnityEvent> _events)) return _events;
        return null;
    }

    //Create Global Events Link
    public void SetGlobalEventLink(IListener listener, UnityEvent _event)
    {
        if (!globalEventsLinks.ContainsKey(listener)) globalEventsLinks[listener] = new List<UnityEvent>();
        globalEventsLinks[listener].Add(_event);
    }



    public List<ITriggerable> GetTriggerablesByLinks(IListener listener)
    {
        if(triggerLinks.TryGetValue(listener, out List<ITriggerable> triggerables)) return triggerables;
        return null;
    }

    public List<IListenerWithCallback> GetCallbacksByLinks(ITriggerable triggerable)
    {
        if (callbackLinks.TryGetValue(triggerable, out List<IListenerWithCallback> listeners)) return listeners;
        return null;
    }



    //TriggerableLink SET
    public void SetTriggerableLink(IListener listener, ITriggerable triggerable)
    {
        if (!triggerLinks.ContainsKey(listener)) triggerLinks[listener] = new List<ITriggerable>();
        triggerLinks[listener].Add(triggerable);
    }

    //CallbackLink SET
    public void SetCallbackLink(IListenerWithCallback callback, ITriggerable triggerable)
    {
        if (!callbackLinks.ContainsKey(triggerable)) callbackLinks[triggerable] = new List<IListenerWithCallback>();
        callbackLinks[triggerable].Add(callback);
    }

}
