using Sarabande.Core;
using Sarabande.Levels;
using Sarabande.Obstacles;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
public class NavigationManager
{

    private HashSet<Vector2Int> _blockedCells = new(); // non-walkables
    private HashSet<(Vector2Int cell, CardinalDirection d)> _thinBlockers = new(); // murs fins normalisés

    private Dictionary<Vector2Int,DynamicObstacle> dynamicObstacles = new();
    private Dictionary<Vector2Int, Portal> portals = new();

    public void SubscribeToEvents()
    {
        NavigationEvents.OnDynamicObstacleRegistry += RegisterDynamicObstacle;
        NavigationEvents.OnDirtyDynamicObstacleUpdate += UpdateDynamicObstacle;


        NavigationEvents.OnQueryCollision += CheckForCollision;
        NavigationEvents.OnQueryPortal += CheckForPortal;

        NavigationEvents.OnRegisterPortal += RegisterPortal;
        NavigationEvents.OnUpdatePortal += UpdatePortal;

        NavigationEvents.OnRegisterStaticObstacle += RegisterStaticObstacle;
    }
    public void UnSubscribeToEvents()
    {
        NavigationEvents.OnDynamicObstacleRegistry -= RegisterDynamicObstacle;
        NavigationEvents.OnDirtyDynamicObstacleUpdate -= UpdateDynamicObstacle;


        NavigationEvents.OnQueryCollision -= CheckForCollision;
        NavigationEvents.OnQueryPortal -= CheckForPortal;

        NavigationEvents.OnRegisterPortal -= RegisterPortal;
        NavigationEvents.OnUpdatePortal -= UpdatePortal;

        NavigationEvents.OnRegisterStaticObstacle -= RegisterStaticObstacle;
    }

    public void RegisterStaticObstacle(ObstacleData c)
    {

        if (c.type == ObstacleData.ObstacleType.Wall)
        {
            _blockedCells.Add(c.cell);
            LogGen.LogAs(this, $"Registered Collider Wall at {c.cell.x}:{c.cell.y}");

        }
        if (c.type == ObstacleData.ObstacleType.ThinWall)
        {
            var item = (c.cell, c.thinWallDirection);
            _thinBlockers.Add(item);
            LogGen.LogAs(this, 
                $"Registered Collider ThinWall at {c.cell.x}:{c.cell.y} oriented {c.thinWallDirection}");
        }
    }

    //deprecated
    private void BuildCollisionSets(List<ObstacleData> obstacleDatas)
    {
        _blockedCells = new HashSet<Vector2Int>();
        _thinBlockers = new HashSet<(Vector2Int, CardinalDirection)>();

        int wall = 0;
        int thinwall = 0;

        foreach (var c in obstacleDatas)
        {
            if (c.type == ObstacleData.ObstacleType.Wall)
            {
                _blockedCells.Add(c.cell);
                wall++;
            }
            if (c.type == ObstacleData.ObstacleType.ThinWall)
            {
                var item = (c.cell, c.thinWallDirection);
                _thinBlockers.Add(item);
                thinwall++;
            }
        }

        Debug.Log("Collision Sets Done " + $": {wall} Walls and {thinwall} ThinWalls Found");
    }

    public bool CheckForPortal(Vector2Int from, Vector2Int to, CardinalDirection _actorDir)
    {
        if(portals.TryGetValue(from, out Portal portal))
        {
            if(portal.ValidExit(_actorDir) && !DynamicObstacleCollision(from, to, _actorDir)) 
                return true;
        }
        return false;
    }

    

    public bool CheckForCollision(Vector2Int from, Vector2Int to, CardinalDirection _actorDir)
    {
        if (WallCollision(to)) return true;
        if (ThinWallCollision(from, to, _actorDir)) return true;
        return DynamicObstacleCollision(from, to, _actorDir);
    }

    private bool WallCollision(Vector2Int cell) => _blockedCells.Contains(cell);


    private bool ThinWallCollision(Vector2Int from, Vector2Int to, CardinalDirection _actorDir) 
    => (_thinBlockers.Contains((from, _actorDir))  || _thinBlockers.Contains((to, GridUtils.Opposite(_actorDir))));


    private bool DynamicObstacleCollision(Vector2Int from, Vector2Int to, CardinalDirection _actorDir)
    {
        bool fromhit = dynamicObstacles.TryGetValue(from, out var obstacleFrom)
            && obstacleFrom.CollidesWith(true, _actorDir);

        bool tohit = dynamicObstacles.TryGetValue(to, out var obstacleTo)
            && obstacleTo.CollidesWith(false, _actorDir);

        return fromhit || tohit;
    }
    private void RegisterPortal(Portal portal)
    {
        portals[portal.Cell] = portal;
        LogGen.LogAs(portal, $"Registered {portal.log}");
    }

    private void UpdatePortal(Portal portal)
    {
        LogGen.LogAs(portal, $"Modified {portal}");
    }


    public void RegisterDynamicObstacle(DynamicObstacle obstacleData)
    {
        dynamicObstacles[obstacleData.Cell] = obstacleData;
        LogGen.LogAs(obstacleData, $"Registered {obstacleData.log}");

    }

    private void UpdateDynamicObstacle(DynamicObstacle obstacleData)
    {
        LogGen.LogAs(obstacleData, $"Modified {obstacleData.log}");
        if (!obstacleData.IsDirty) return;

        var item = dynamicObstacles.First(kvp => kvp.Value == obstacleData);
        if (item.Key != obstacleData.Cell)
        {
            LogGen.LogAs(this, $"Dynamic Obstacle is Dirty, Fixed old reference at {item.Key}");
            dynamicObstacles.Remove(item.Key);
            dynamicObstacles[obstacleData.Cell] = obstacleData;
        }

        obstacleData.ResetDirtyFlag();
    }

}


