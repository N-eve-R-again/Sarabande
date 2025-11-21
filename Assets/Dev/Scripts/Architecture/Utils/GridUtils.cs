using Sarabande.Levels;
using System.Collections.Generic;
using UnityEngine;

namespace Sarabande.Core
{
    public static class GridUtils
    {
        // --- Conversions ---
        public static Vector2Int ToV2(this GridCoord c) => new Vector2Int(c.x, c.z);
        public static GridCoord ToGridCoord(this Vector2Int v) => new GridCoord(v.x, v.y);

        // --- Géométrie grille ---
        public static Vector3 Center(Vector2Int c, float cellSize)
            => new Vector3((c.x + 0.5f) * cellSize, 0f, (c.y + 0.5f) * cellSize);
        public static Vector3 CenterGrid(Vector2Int c, float cellSize = 1f)
            => Center(c, cellSize);

        public static Vector3 CenterXZ(Vector2Int cell)
        {
            float x = (cell.x + 0.5f) * LevelGlobalSettings.cellSize;
            float z = (cell.y + 0.5f) * LevelGlobalSettings.cellSize;
            return new Vector3(x, 0f, z);
        }

        public static CardinalDirection GetCardinalDirection(GridCoord coord)
        {
            if (coord.x != 0)
            {
                if (coord.x > 0)
                    return CardinalDirection.East;

                return CardinalDirection.West;

            }

            if (coord.z != 0)
            {
                if (coord.z > 0)
                    return CardinalDirection.North;

                return CardinalDirection.South;
            }

            return CardinalDirection.North; //North par défaut;
        }

        public static bool InsideBounds(Vector2Int c, int width, int height)
            => c.x >= 0 && c.x < width && c.y >= 0 && c.y < height;

        public static IEnumerable<Vector2Int> CardinalNeighbors(Vector2Int c)
        {
            yield return new Vector2Int(c.x + 1, c.y);
            yield return new Vector2Int(c.x - 1, c.y);
            yield return new Vector2Int(c.x, c.y + 1);
            yield return new Vector2Int(c.x, c.y - 1);
        }

        // --- Directions ---
        public static Vector2Int DirToVec(CardinalDirection d) => d switch
        {
            CardinalDirection.North => Vector2Int.up,
            CardinalDirection.East => Vector2Int.right,
            CardinalDirection.South => Vector2Int.down,
            CardinalDirection.West => Vector2Int.left,
            _ => Vector2Int.zero
        };

        public static Vector3 DirToWorld(CardinalDirection d) => d switch
        {
            CardinalDirection.North => new Vector3(0f, 0f, 1f),
            CardinalDirection.East => new Vector3(1f, 0f, 0f),
            CardinalDirection.South => new Vector3(0f, 0f, -1f),
            CardinalDirection.West => new Vector3(-1f, 0f, 0f),
            _ => Vector3.forward
        };

        public static CardinalDirection Opposite(CardinalDirection d) => d switch
        {
            CardinalDirection.North => CardinalDirection.South,
            CardinalDirection.South => CardinalDirection.North,
            CardinalDirection.East => CardinalDirection.West,
            CardinalDirection.West => CardinalDirection.East,
            _ => d
        };


        // --- Arêtes (pour murs fins, grid gates, etc.) ---
        public static (Vector2Int a, Vector2Int b) NormalizeEdge(Vector2Int a, Vector2Int b)
        {
            if (a.x < b.x) return (a, b);
            if (a.x > b.x) return (b, a);
            return (a.y <= b.y) ? (a, b) : (b, a);
        }

        public static (Vector2Int a, Vector2Int b) EdgeOf(Vector2Int a, CardinalDirection side)
            => NormalizeEdge(a, a + DirToVec(side));

        public static bool IsAdjacentCardinal(Vector2Int a, Vector2Int b)
            => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y) == 1;
    }
}
