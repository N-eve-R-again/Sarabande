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

        // --- Arrow Traps (proto) -----------------------------------------------------

        [System.Serializable]
        public struct ArrowTrapSpec
        {
            public GridCoord triggerCell;    // case walkable à fouler (HERO ou NME)
            public GridCoord startCell;      // première case "dedans la map" que la flèche traverse
            public EdgeDirection travelDir;  // direction de déplacement de la flèche (N/E/S/W)
            [Min(0.1f)] public float arrowSpeed; // vitesse en unités monde / sec (à tweaker dans l'Inspector)

            // --- options de réarmement ---
            public bool canRearm;             // si true, le piège se réarme
            [Min(0f)] public float rearmDelay; // temps avant réarmement (secondes)
        }

        [Tooltip("Pièges à flèche : quand on marche sur 'triggerCell', une flèche part de 'startCell' dans 'travelDir'.")]
        public List<ArrowTrapSpec> arrowTraps = new();
    }
}
