using Sarabande.Core;
using Sarabande.Levels;
using System.Collections.Generic;
using UnityEngine;
public class NavigationManager
{

    private HashSet<Vector2Int> _blockedCells = new(); // non-walkables
    private HashSet<(Vector2Int cell, CardinalDirection d)> _thinBlockers = new(); // murs fins normalisés

    private Dictionary<Vector2Int,DynamicObstacle> dynamicObstacles = new();
    private Dictionary<Vector2Int, Portal> portals = new();

    public void SubscribeToEvents()
    {
        NavigationEvents.OnRegisterDynamicObstacle += RegisterDynamicObstacle;
        NavigationEvents.OnModifyDynamicObstacle += ModifyDynamicObstacle;
        NavigationEvents.OnMoveDynamicObstacle += MoveDynamicObstacle;

        NavigationEvents.OnQueryCollision += CheckForCollision;
        NavigationEvents.OnQueryPortal += CheckForPortal;

        NavigationEvents.OnRegisterPortal += RegisterPortal;
        NavigationEvents.OnModifyPortal += ModifyPortal;

        NavigationEvents.OnRegisterStaticObstacle += RegisterStaticObstacle;
    }
    public void UnSubscribeToEvents()
    {
        NavigationEvents.OnRegisterDynamicObstacle -= RegisterDynamicObstacle;
        NavigationEvents.OnModifyDynamicObstacle -= ModifyDynamicObstacle;
        NavigationEvents.OnMoveDynamicObstacle -= MoveDynamicObstacle;

        NavigationEvents.OnQueryCollision -= CheckForCollision;
        NavigationEvents.OnQueryPortal -= CheckForPortal;

        NavigationEvents.OnRegisterPortal -= RegisterPortal;
        NavigationEvents.OnModifyPortal -= ModifyPortal;

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
            if (!portal.activated)
            {

                return false;
            }
            if(_actorDir != portal.direction) return false;
            if (DynamicObstacleCollision(from, to, _actorDir))
            {
                Debug.Log("Dynamic here");
                return false;
            }
            else
            {
                Debug.Log("No dynamic in between");
            }

            Debug.Log("test");
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
    private void RegisterPortal(ExitConfig portalConfig)
    {
        LogGen.LogAs(this, $"Portal register at {portalConfig.cell} with direction {portalConfig.direction} and state {portalConfig.startState}");
        portals[portalConfig.cell] = new Portal(portalConfig.direction, portalConfig.startState);
    }

    private void ModifyPortal(Vector2Int cell, bool newState)
    {
        if(portals.TryGetValue(cell, out var portal))
        {
            LogGen.ErrorAs(this, $"Portal at {cell} was changed to {newState}");
            portal.activated = newState;
        }
        else
        {
            LogGen.ErrorAs(this, $"Couldn't find Portal at {cell}");
        }
    }

    public void RegisterDynamicObstacle(ObstacleData obstacleData, bool originalState = true)
    {
        bool isthin = (obstacleData.type == ObstacleData.ObstacleType.ThinWall);
        DynamicObstacle temp = new DynamicObstacle(isthin, obstacleData.thinWallDirection, originalState);
        dynamicObstacles[obstacleData.cell] = temp;
        if (!isthin)
        {
            LogGen.LogAs(this, $"Registered Dynamic Wall at Cell {obstacleData.cell.x}:{obstacleData.cell.y} (original state : {originalState})");
        }
        else
        {
            LogGen.LogAs(this, $"Registered Dynamic ThinWall at Cell {obstacleData.cell.x}:{obstacleData.cell.y} (original state : {originalState})");
        }
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
public class Portal
{
    public CardinalDirection direction;
    public bool activated;

    public Portal(CardinalDirection direction, bool activated)
    {
        this.direction = direction;
        this.activated = activated;
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

