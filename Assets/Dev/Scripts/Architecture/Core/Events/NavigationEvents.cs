using Sarabande.Core;
using Sarabande.Obstacles;
using System;
using UnityEngine;

public static class NavigationEvents
{
    public static event Action<ObstacleData> OnRegisterStaticObstacle;

    [Obsolete("Utilise this.RegisterObstacle() via l'extension IObstacle.")]
    public static void Raise_StaticObstacleRegistry(ObstacleData _obstacleData)
    => OnRegisterStaticObstacle?.Invoke(_obstacleData);


    public static event Action<ExitConfig> OnRegisterPortal;
    [Obsolete("Utilise this.RegisterPortal() via l'extension IPortalCreator.")]
    public static void NotifyPortalRegistry(ExitConfig _portalConfig)
    => OnRegisterPortal.Invoke(_portalConfig);

    public static event Action<Vector2Int, bool> OnModifyPortal;

    [Obsolete("Utilise this.ModifyPortal() via l'extension IPortalCreator.")]
    public static void NotifyPortalModification(Vector2Int key, bool newActivatedState)
    => OnModifyPortal?.Invoke(key, newActivatedState);



    public static event Action<DynamicObstacle> OnDynamicObstacleRegistry;
    public static void Raise_DynamicObstacleRegistry(DynamicObstacle dynamicObstacle)
    => OnDynamicObstacleRegistry?.Invoke(dynamicObstacle);

    public static event Action<DynamicObstacle> OnDirtyDynamicObstacleUpdate;
    public static void Raise_DynamicObstacleUpdate(DynamicObstacle dynamicObstacle)
    => OnDirtyDynamicObstacleUpdate?.Invoke(dynamicObstacle);


    public static event Func<Vector2Int, Vector2Int, CardinalDirection, bool> OnQueryCollision;
    public static bool QueryCollision(Vector2Int from, Vector2Int to, CardinalDirection actorDir)
    => OnQueryCollision?.Invoke(from, to, actorDir) ?? false;

    public static event Func<Vector2Int, Vector2Int, CardinalDirection, bool> OnQueryPortal;
    public static bool QueryExitPortal(Vector2Int from, Vector2Int to, CardinalDirection actorDir)
    => OnQueryPortal?.Invoke(from, to, actorDir) ?? false;

}