using Sarabande.Core;
using Sarabande.Levels;
using Sarabande.Messages;
using System;
using System.Collections.Generic;
using UnityEngine;

public class LevelEntitiesManager : MonoBehaviour, IClearable
{
    public List<EventTriggerable> eventTrig = new List<EventTriggerable>();

    [Header("SubManagers")]
    [SerializeField] private MessageSystem messageSystem;
    private InteractionSystem interactionSystem;
    private NavigationManager navigationManager;

    private void Init(LevelData levelData)
    {
        // S'abonner aux events
        interactionSystem = new InteractionSystem();
        navigationManager = new NavigationManager();
        navigationManager.SubscribeToEvents();

        navigationManager.BuildCollisionSets(levelData);

        if (messageSystem != null) messageSystem.SubscribeToEvents();

        Debug.Log("LEM Subscribed to LevelEntityEvents");
    }


    public void Ready(LevelData _levelData)
    {
        if(messageSystem == null) throw new MissingReferenceException("MessageSystem");
        //Instance = this;
        Init(_levelData);
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