using Sarabande.Core;
using Sarabande.Messages;
using System;
using System.Collections.Generic;
using UnityEngine;

public class LevelEntitiesManager : MonoBehaviour, IClearable
{


    private static LevelEntitiesManager Instance;
    public static LevelEntitiesManager I => Instance;

    [Header("SubManagers")]
    [SerializeField] private MessageSystem messageSystem;
    private InteractionSystem interactionSystem;

    public MessageSystem GetMessageSystem() => messageSystem;

    private void SubscribeToEvents()
    {
        // S'abonner aux events
        interactionSystem = new InteractionSystem();

        LevelEntityEvents.OnListenerRegistry += interactionSystem.RegisterListener;
        LevelEntityEvents.OnListenerTryCallTrigger += interactionSystem.SendEventToTriggerable;
        LevelEntityEvents.OnTryTriggerLinkRegistry += interactionSystem.RegisterTriggerLink;
        LevelEntityEvents.OnTriggerableRegistry += interactionSystem.RegisterTriggerable;
        ActorEvents.OnActorMove += interactionSystem.ActorMoved;
        Debug.Log("LEM Subscribed to LevelEntityEvents");
    }

    private void OnDisable()
    {
        // Se désabonner (important pour éviter les fuites mémoire!)
        LevelEntityEvents.OnListenerRegistry -= interactionSystem.RegisterListener;
        LevelEntityEvents.OnListenerTryCallTrigger -= interactionSystem.SendEventToTriggerable;
        LevelEntityEvents.OnTryTriggerLinkRegistry -= interactionSystem.RegisterTriggerLink;
        LevelEntityEvents.OnTriggerableRegistry -= interactionSystem.RegisterTriggerable;
        ActorEvents.OnActorMove -= interactionSystem.ActorMoved;
        Debug.Log("LEM Unsubscribed to LevelEntityEvents");
    }

    public void Ready()
    {
        if(messageSystem == null) throw new MissingReferenceException("MessageSystem");
        Instance = this;
        SubscribeToEvents();
    }

    public void ClearObject()
    {
        //reset tout les objects ici
    }

}


[Serializable]
public class DictionaryInInspector
{
    [SerializeField] private List<string> debugKeys = new();
    [SerializeField] private List<string> debugValues = new();

    public void UpdateDictionary<TKey,TValue>(Dictionary<TKey, TValue> dico)
    {
        debugKeys.Clear();
        debugValues.Clear();

        foreach (var element in dico)
        {
            debugKeys.Add(element.Key?.ToString() ?? "null");
            debugValues.Add(element.Value?.ToString() ?? "null");
        }
    }

}