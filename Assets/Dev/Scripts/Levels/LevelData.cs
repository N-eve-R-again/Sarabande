using System.Collections.Generic;
using UnityEngine;
using Sarabande.Core;

namespace Sarabande.Levels
{
    /// <summary>
    /// Données d'un niveau (proto) : dimensions, murs, murs fins, spawns, sortie.
    /// On étendra plus tard pour les leviers, pièges, etc.
    /// </summary>
    [CreateAssetMenu(menuName = "Sarabande/Level Data", fileName = "LevelData")]
    public class LevelData : ScriptableObject
    {
        [Min(1)] public int width = 8;     // colonnes (A..H)
        [Min(1)] public int height = 8;    // rangées  (1..8)
        [Tooltip("Cases non walkable (murs)")]
        public List<GridCoord> nonWalkables = new();

        [Tooltip("Murs fins entre deux cases adjacentes")]
        public List<EdgeBlocker> thinWalls = new();

        [Header("Spawns")]
        public GridCoord heroSpawn;  // D8 = (3,7)
        public GridCoord nmeSpawn;   // B7 = (1,6)

        [Header("Entry")]
        [Tooltip("Depuis quel bord le héros arrive pour son entry step")]
        public EdgeDirection heroEntry = EdgeDirection.North;

        [Header("Sortie")]
        public EdgeExit exit;        // H1 vers East
    }
}
