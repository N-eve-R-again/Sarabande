using Sarabande.Core;
using System;
using UnityEngine;

namespace Sarabande.Obstacles { 

    [Serializable]
    public class ObstacleData
    {

        public enum ObstacleType
        {
            Wall,
            ThinWall
        }
        public Vector2Int cell;
        public ObstacleType type = ObstacleType.Wall;
        public CardinalDirection thinWallDirection = CardinalDirection.North;

        public ObstacleData(ObstacleType type, Vector2Int cell, CardinalDirection thinWallDirection = CardinalDirection.South)
        {
            this.type = type;
            this.cell = cell;
            this.thinWallDirection = thinWallDirection;
        }

        public ObstacleData()
        {

        }

        public static explicit operator ObstacleData(EdgeBlocker edgeBlocker)
        {

            CardinalDirection dir = CardinalDirection.North;
            Vector2Int cell = edgeBlocker.a;
            if (edgeBlocker.a.x == edgeBlocker.b.x)
            {
                dir = CardinalDirection.North;
                cell = Vector2Int.Min(edgeBlocker.a, edgeBlocker.b);
            }
            if (edgeBlocker.a.z == edgeBlocker.b.z)
            {
                dir = CardinalDirection.East;
                cell = Vector2Int.Min(edgeBlocker.a, edgeBlocker.b);
            }
            return new ObstacleData(ObstacleType.ThinWall, cell, dir);
        }


    }
}
