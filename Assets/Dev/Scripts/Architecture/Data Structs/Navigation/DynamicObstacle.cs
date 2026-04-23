using Sarabande.Core;
using UnityEngine;

namespace Sarabande.Obstacles
{
    [System.Serializable]
    public class DynamicObstacle
    {
        [SerializeField] private Vector2Int cell;
        [SerializeField] private bool thin;
        [SerializeField] private CardinalDirection direction;
        [SerializeField] private bool activated;
        private string name => thin ? $"{direction} oriented Thin Wall" : $"Wall";
        private bool dirty = false;

        public Vector2Int Cell => cell;
        public bool IsActivated => activated;
        public string Name => name;
        public bool IsDirty => dirty;
        public void ResetDirtyFlag() => dirty = false;
        private void SetDirty() => dirty = true;

        public bool CollidesWith(bool isExitCheck, CardinalDirection _actorDir)
        {
            if (!activated) return false; //si je suis désactivé on m'ignore.

            if (!thin)  // si je suis un wall
                return !isExitCheck; //je collisionne que sur un EnterCheck( !isExitCheck)

            //je suis un thin wall
            return _actorDir == (isExitCheck ? direction : GridUtils.Opposite(direction));// si je suis en !ExitCheck alors j'inverse la direction qui bloque
        }

        public DynamicObstacle(Vector2Int cell, ObstacleData.ObstacleType type, CardinalDirection direction, bool activated)
        {
            this.cell = cell;
            this.thin = type == ObstacleData.ObstacleType.ThinWall;

            this.activated = activated;
            this.direction = direction;
        }

        public void SetActivated(bool activated)
        {
            this.activated = activated;
            this.UpdateDynamicObstacle();
        }

        public void ChangeCell(Vector2Int newCell)
        {
            this.cell = newCell;
            this.SetDirty();
            this.UpdateDynamicObstacle();
        }


    }


#pragma warning disable CS0618
    public static class DynamicObstacleEvents
    {
        public static void RegisterDynamicObstacle(this DynamicObstacle dynamicObstacle)
            => NavigationEvents.Raise_DynamicObstacleRegistry(dynamicObstacle);

        public static void UpdateDynamicObstacle(this DynamicObstacle dynamicObstacle)
        => NavigationEvents.Raise_DynamicObstacleUpdate(dynamicObstacle);

    }
#pragma warning restore CS0618
}
