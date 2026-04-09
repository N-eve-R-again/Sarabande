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

    public void Init()
    {
        registryDatabase = new RegistryDatabase();

        interactionSystem = new InteractionSystem();
        interactionSystem.SetDatabase(registryDatabase);

        entityRegister = new EntityRegister();
        entityRegister.SetDatabase(registryDatabase);

        navigationManager = new NavigationManager();
        navigationManager.SubscribeToEvents();

        SubscribeToLevelEntityEvents();
        SubscribeToRegistyEvents();
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
        => entityRegister.RegisterSensor(cell, extension);
    private void RegisterTriggerable(ITriggerable triggerable) 
        => entityRegister.RegisterTriggerable(triggerable);
    private void RegisterListener(IListener listener) 
        => entityRegister.RegisterListener(listener);
    private void ActorMoved(IActor actor, ActorInteractionData data) 
        => interactionSystem.ActorMoved(actor, data);
    private void SendSignal(string[] signal) 
        => interactionSystem.SendSignal(signal);   
    private void SendTriggerableCallback(ITriggerable triggerable)
        => interactionSystem.SendTriggerableCallback(triggerable);
    private void SendEventToTriggerable(IListener listener)
        => interactionSystem.SendEventToTriggerable(listener);
}
