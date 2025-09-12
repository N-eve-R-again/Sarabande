using System.Collections.Generic;
using UnityEngine;
using Sarabande.Core;
using UnityEngine.Serialization;

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

        [Header("Décor (murs traversables)")]
        public List<GridCoord> passThroughWalls = new();  // ex: A6, etc.



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

        // --- Timed Doors & Levers ---

        [System.Serializable]
        public class TimedDoorSpec
        {
            public GridCoord cell;                  // ex: B5
            [Min(0.1f)] public float openSeconds = 3f;  // durée d'ouverture de base
        }
        public List<TimedDoorSpec> timedDoors = new();

        [System.Serializable]
        public class LeverSpec
        {
            public GridCoord cell;                     // ex: E7
            public EdgeDirection requireFacing = EdgeDirection.North; // direction à pousser
            [Min(0)] public int linkedDoorIndex = 0;   // index dans la liste 'timedDoors'
        }
        public List<LeverSpec> levers = new();

        // --- Messages ---

        [Header("Messages")]
        public List<MessageSpec> messages = new();

        [System.Serializable]
        public class MessageSpec
        {
            // ?? NOUVEAU champ cohérent avec le reste du projet
            public GridCoord cell;        // utilise x / z comme partout

            [TextArea(2, 5)] public string text;
            [Min(0.1f)] public float displaySeconds = 3f;
            public AudioClip voiceClip;
        }

        // --- Dalles Disco ---

        [Header("Disco Start")]
        [Tooltip("Cases qui déclenchent la séquence disco quand le HÉRO y entre.")]
        public List<GridCoord> discoStartTiles = new();

        [System.Serializable]
        public class DiscoSequenceSpec
        {
            // Strict order of tiles to step on
            public List<GridCoord> cells = new List<GridCoord>();

            // Per-step timers; if length mismatches, use defaultStepSeconds
            public List<float> stepSeconds = new List<float>();

            // Auto-start when this timed door opens; -1 = no auto-start
            public int startOnDoorIndex = -1;

            // Default if stepSeconds[i] missing
            public float defaultStepSeconds = 0.8f;
        }

        public List<DiscoSequenceSpec> discoSequences = new List<DiscoSequenceSpec>();

        // --- Grid Gates (barreaux sur une ARÊTE, bloquent le passage mais pas la LOS / flèches) ---
        [System.Serializable]
        public class GridGateSpec
        {
            public GridCoord cell;                 // case A
            public EdgeDirection side;             // bord de A (vers B = A + side)
            public bool initiallyOpen = false;     // fermé par défaut (bloque), sinon déjà ouvert
        }
        public List<GridGateSpec> gridGates = new();


    }
}
