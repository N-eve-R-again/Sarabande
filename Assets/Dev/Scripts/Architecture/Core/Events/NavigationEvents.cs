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


    public static event Action<Portal> OnRegisterPortal;
    [Obsolete("Utilise this.RegisterPortal() via la classe Portal.")]
    public static void Raise_PortalRegistry(Portal _portalConfig)
    => OnRegisterPortal.Invoke(_portalConfig);

    public static event Action<Portal> OnUpdatePortal;
    [Obsolete("Utilise this.UpdatePortal() via la classe Portal.")]
    public static void Raise_PortalUpdate(Portal _portalConfig)
    => OnUpdatePortal.Invoke(_portalConfig);


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