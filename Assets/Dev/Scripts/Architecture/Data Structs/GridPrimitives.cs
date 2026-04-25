using Sarabande.Core;
using Sarabande.Levels;
using System.Drawing;
using UnityEngine;

namespace Sarabande.Core
{
    /// <summary>Coordonnée de grille (x,z) avec A1 = (0,0), H8 = (7,7)</summary>
    [System.Serializable]
    public struct GridCoord
    {
        public int x; // colonne (A=0 ... H=7)
        public int z; // rangée   (1=0 ... 8=7)

        public GridCoord(int x, int z) { this.x = x; this.z = z; }

        public static implicit operator Vector2Int(GridCoord coord)
        {
            return new Vector2Int(coord.x, coord.z);
        }

        public static implicit operator GridCoord(Vector2Int coord)
        {
            return new GridCoord(coord.x, coord.y);
        }
        public override string ToString() => $"({x}_{z})";
    }

    /// <summary>Direction cardinale pour les sorties/murs fins</summary>
    public enum CardinalDirection { North, East, South, West }

    /// <summary>Mur fin entre deux cases adjacentes</summary>
    [System.Serializable]
    public struct EdgeBlocker
    {
        public GridCoord a;   // ex: D2 = (3,1)
        public GridCoord b;   // ex: E2 = (4,1)
    }

}
