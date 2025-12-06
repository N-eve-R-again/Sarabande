using Sarabande.Core;
using Sarabande.Levels;
using System;
using System.Collections.Generic;
using UnityEngine;

public class NavigationManager
{
    private HashSet<Vector2Int> _blockedCells; // non-walkables
    private HashSet<(Vector2Int cell, CardinalDirection d)> _thinBlockers; // murs fins normalisés

    private Dictionary<Vector2Int,DynamicObstacle> dynamicObstacles = new();
    
    public void SubscribeToEvents()
    {
        NavigationEvents.OnRegisterDynamicObstacle += RegisterDynamicObstacle;
        NavigationEvents.OnModifyDynamicObstacle += ModifyDynamicObstacle;
        NavigationEvents.OnMoveDynamicObstacle += MoveDynamicObstacle;
        NavigationEvents.OnQueryCollision += CheckForCollision;
    }
    public void UnSubscribeToEvents()
    {
        NavigationEvents.OnRegisterDynamicObstacle -= RegisterDynamicObstacle;
        NavigationEvents.OnModifyDynamicObstacle -= ModifyDynamicObstacle;
        NavigationEvents.OnMoveDynamicObstacle -= MoveDynamicObstacle;
        NavigationEvents.OnQueryCollision -= CheckForCollision;
    }
    private void OnDestroy()
    {
        UnSubscribeToEvents();
    }

    public void BuildCollisionSets(LevelData levelData)
    {
        _blockedCells = new HashSet<Vector2Int>();
        _thinBlockers = new HashSet<(Vector2Int, CardinalDirection)>();

        foreach (var c in levelData.obstacles)
        {
            if (c.type == ObstacleData.ObstacleType.Wall)
            {
                _blockedCells.Add(c.cell);
            }
            if (c.type == ObstacleData.ObstacleType.ThinWall)
            {
                var item = (c.cell, c.thinWallDirection);
                _thinBlockers.Add(item);
            }
        }
    }
    public bool CheckForCollision(Vector2Int from, Vector2Int to, CardinalDirection _actorDir)
    {
        if (WallCollision(to)) return true;
        if (ThinWallCollision(from, to, _actorDir)) return true;
        return DynamicObstacleCollision(from, to, _actorDir);
    }

    private bool WallCollision(Vector2Int cell)
    {
        return _blockedCells.Contains(cell);
    }

    private bool ThinWallCollision(Vector2Int from, Vector2Int to, CardinalDirection _actorDir)
    {

        return 
            (
            _thinBlockers.Contains((from, _actorDir))
            ||
            _thinBlockers.Contains((to, GridUtils.Opposite(_actorDir)))
            );

    }

    private bool DynamicObstacleCollision(Vector2Int from, Vector2Int to, CardinalDirection _actorDir)
    {
        bool fromhit = dynamicObstacles.TryGetValue(from, out var obstacleFrom)
            && obstacleFrom.CollidesWith(true, _actorDir);

        bool tohit = dynamicObstacles.TryGetValue(to, out var obstacleTo)
            && obstacleTo.CollidesWith(false, _actorDir);

        return fromhit || tohit;
    }


    public void RegisterDynamicObstacle(ObstacleData obstacleData, bool originalState = true)
    {
        bool isthin = (obstacleData.type == ObstacleData.ObstacleType.ThinWall);
        DynamicObstacle temp = new DynamicObstacle(isthin, obstacleData.thinWallDirection, originalState);
        dynamicObstacles[obstacleData.cell] = temp;
    }

    private void ModifyDynamicObstacle(Vector2Int cell, bool newState)
    {
        if (dynamicObstacles.TryGetValue(cell, out var obstacle))
        {
            obstacle.SetActivated(newState);
        }
    }

    private void MoveDynamicObstacle(Vector2Int oldCell, Vector2Int newCell)
    {
        if (dynamicObstacles.TryGetValue(oldCell, out DynamicObstacle obstacle))
        {
            dynamicObstacles.Remove(oldCell);
            dynamicObstacles[newCell] = obstacle;
        }
    }
}

public class DynamicObstacle
{
    private bool thin;
    private CardinalDirection direction;
    private bool activated;

    public bool CollidesWith(bool isExitCheck, CardinalDirection _actorDir)
    {
        if (!activated) return false; //si je suis désactivé on m'ignore.

        if (!thin)  // si je suis un wall
            return !isExitCheck; //je collisionne que sur un EnterCheck( !isExitCheck)

        //je suis un thin wall
        return _actorDir == (isExitCheck ? direction : GridUtils.Opposite(direction));// si je suis en !ExitCheck alors j'inverse la direction qui bloque
    }

    public DynamicObstacle(bool thin, CardinalDirection direction, bool OriginalState)
    {
        this.thin = thin;
        activated = OriginalState;
        this.direction = direction;
    }

    public void SetActivated(bool activated)
    {
        this.activated = activated;
    }
}

