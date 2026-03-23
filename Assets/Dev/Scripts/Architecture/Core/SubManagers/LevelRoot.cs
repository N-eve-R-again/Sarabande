using Sarabande.Levels;
using Sarabande.Messages;
using System;
using UnityEngine;

public class LevelRoot : MonoBehaviour
{
    private EntityRegister entityRegister;
    private RegistryDatabase registryDatabase;
    private InteractionSystem interactionSystem;
    private NavigationManager navigationManager;

    [Header("SubManagers")]
    public MessageSystem messageSystem;

    public void Init()
    { 
        interactionSystem = new InteractionSystem();
        navigationManager = new NavigationManager();

        registryDatabase = new RegistryDatabase();
        entityRegister = new EntityRegister();


        navigationManager.SubscribeToEvents();
        SubscribeToLevelEntityEvents();
        SubscribeToRegistyEvents();
    }

    public void BuildCollisionSets(LevelData levelData)
    {
        navigationManager.BuildCollisionSets(levelData);

    }

    private void OnDisable()
    {
        UnSubscribeToLevelEntityEvents();
        UnSubscribeToRegistyEvents();
    }

    private void SubscribeToLevelEntityEvents()
    {
        LevelEntityEvents.OnListenerTryCallTrigger += SendEventToTriggerable;
        LevelEntityEvents.OnTriggerableCallback += SendTriggerableCallback;

        LevelEntityEvents.OnSignalSent += SendSignal;

        ActorEvents.OnActorMove += ActorMoved;
    }
    private void UnSubscribeToLevelEntityEvents()
    {
        LevelEntityEvents.OnListenerTryCallTrigger -= SendEventToTriggerable;
        LevelEntityEvents.OnTriggerableCallback -= SendTriggerableCallback;

        LevelEntityEvents.OnSignalSent -= SendSignal;

        ActorEvents.OnActorMove -= ActorMoved;
    }
    private void SubscribeToRegistyEvents()
    {
        RegistryEvents.OnListenerRegistry += RegisterListener;
        RegistryEvents.OnTriggerableRegistry += RegisterTriggerable;
        RegistryEvents.OnSensorRegistry += RegisterSensor;
    }
    private void UnSubscribeToRegistyEvents()
    {
        RegistryEvents.OnListenerRegistry -= RegisterListener;
        RegistryEvents.OnTriggerableRegistry -= RegisterTriggerable;
        RegistryEvents.OnSensorRegistry -= RegisterSensor;
    }


    private void RegisterSensor(Vector2Int cell, ISensorExtension extension) 
        => entityRegister.RegisterSensor(registryDatabase, cell, extension);
    private void RegisterTriggerable(ITriggerable triggerable) 
        => entityRegister.RegisterTriggerable(registryDatabase, triggerable);
    private void RegisterListener(IListener listener) 
        => entityRegister.RegisterListener(registryDatabase, listener);
    private void ActorMoved(IActor actor, ActorInteractionData data) 
        => interactionSystem.ActorMoved(registryDatabase, actor, data);
    private void SendSignal(string[] signal) 
        => interactionSystem.SendSignal(registryDatabase, signal);   
    private void SendTriggerableCallback(ITriggerable triggerable)
        => interactionSystem.SendTriggerableCallback(registryDatabase, triggerable);
    private void SendEventToTriggerable(IListener listener)
        => interactionSystem.SendEventToTriggerable(registryDatabase, listener);
}
