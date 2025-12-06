using Sarabande.Core;
using Sarabande.Levels;
using System;
using UnityEngine;

public class LevelEntityEvents
{

    // Quand un listener essaye d'activer un triggerable
    public static event Action<IListener> OnListenerTryCallTrigger; // Event
    public static void NotifyListenerTryCallTrigger(IListener listener) // Fonction Call
    => OnListenerTryCallTrigger?.Invoke(listener);

    // Quand un Triggerable veut Callback
    public static event Action<ITriggerable> OnTriggerableCallback;
    public static void NotifyTriggerableCallback(ITriggerable triggerable)
    => OnTriggerableCallback?.Invoke(triggerable);

    public static event Action<ISignalerEnxtension> OnSignalSent;
    public static void SendSignal(ISignalerEnxtension signer)
    => OnSignalSent?.Invoke(signer);
}

public static class RegistryEvents
{
    // Quand un listener est créé
    public static event Action<Vector2Int, IListener> OnListenerRegistry; // Event
    public static void NotifyListenerRegistry(Vector2Int cell, IListener listener) // Fonction Call
    => OnListenerRegistry?.Invoke(cell, listener);

    public static event Action<Vector2Int, ISensorExtension> OnSensorRegistry; // Event
    public static void NotifySensorRegistry(Vector2Int cell, ISensorExtension sensor) // Fonction Call
    => OnSensorRegistry?.Invoke(cell, sensor);

    // Quand un triggerable est créé
    public static event Action<string, ITriggerable> OnTriggerableRegistry; // Event
    public static void NotifyTriggerableRegistry(string triggerableKey, ITriggerable triggerable) // Fonction Call
    => OnTriggerableRegistry?.Invoke(triggerableKey, triggerable);


    // Quand un listener veut créer un triggerlink
    public static event Action<string, IListener> OnTryTriggerLinkRegistry; // Event
    public static void NotifyTryTriggerLinkRegistry(string _triggerKey, IListener listener) // Fonction Call
    => OnTryTriggerLinkRegistry?.Invoke(_triggerKey, listener);


}

public static class ActorEvents
{
    //Quand un actor fait un move
    public static event Action<IActor, ActorInteractionData> OnActorMove; // Event
    public static void NotifyActorMove(IActor _actor, ActorInteractionData _interaction) // Fonction Call
        => OnActorMove?.Invoke(_actor, _interaction);

}

public static class UIEvents
{
    public static event Func<MessageConfig,int> OnRegisterMsgCollectible;
    public static int NotifyRegisterMsgCollectible(MessageConfig _messageConfig)
        => OnRegisterMsgCollectible?.Invoke(_messageConfig) ?? -1;

    public static event Action<int> OnCollectMsgCollectible;
    public static void NotifyCollectMsgCollectible(int _messageConfig)
        => OnCollectMsgCollectible?.Invoke(_messageConfig);
}

public static class NavigationEvents
{

    public static event Action<ObstacleData, bool> OnRegisterDynamicObstacle;

    public static void NotifyDynamicObstacle(ObstacleData obstacleData, bool originalState = true)
    => OnRegisterDynamicObstacle?.Invoke(obstacleData, originalState);

    public static event Action<Vector2Int, bool> OnModifyDynamicObstacle;
    public static void NotifyDynamicObstacleModification(Vector2Int key, bool newActivatedState)
    => OnModifyDynamicObstacle?.Invoke(key, newActivatedState);

    public static event Action<Vector2Int, Vector2Int> OnMoveDynamicObstacle;
    public static void NotifyDynamicObstacleMove(Vector2Int key, Vector2Int newKey)
    => OnMoveDynamicObstacle?.Invoke(key, newKey);

    public static event Func<Vector2Int, Vector2Int, CardinalDirection, bool> OnQueryCollision;

    public static bool QueryCollision(Vector2Int from, Vector2Int to, CardinalDirection actorDir)
    => OnQueryCollision?.Invoke(from, to, actorDir) ?? false;
}