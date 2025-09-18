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

            // --- NOUVEAU : liste d'émissions avancées ---
            public List<ArrowEmission> emissions;  // si null/empty => fallback sur le comportement legacy 1 flèche
        }

        [System.Serializable]
        public class ArrowEmission
        {
            [Header("Origin & Direction")]
            public GridCoord startCell;                          // origine (peut différer du spec.startCell legacy)
            public Sarabande.Core.EdgeDirection travelDir;      // direction de cette emission
            [UnityEngine.Min(0.05f)] public float arrowSpeed = 6f;

            [Header("Fixed times (absolute, after trigger)")]
            public List<float> shotTimes;                       // ex: [0, 2, 8] => 3 flèches à 0s, 2s, 8s

            [Header("Repeating schedule")]
            public bool repeat = false;                         // si true, on déclenche un pattern répétitif
            [UnityEngine.Min(0f)] public float repeatStartDelay = 0f;
            [UnityEngine.Min(0.01f)] public float repeatInterval = 0.2f;

            [Tooltip("Si > 0, répète pendant cette durée (en s). Ignoré si repeatCount > 0.")]
            [UnityEngine.Min(0f)] public float repeatDuration = 0f;

            [Tooltip("Si > 0, tire exactement N fois (intervalle constant). Prend le pas sur repeatDuration.")]
            [UnityEngine.Min(0)] public int repeatCount = 0;
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

        // --- TRIGGERS GÉNÉRIQUES (Dalles + Table de routage) ---------------------------
        [System.Serializable]
        public class TriggerPadSpec
        {
            public GridCoord cell;              // case de la dalle
            public bool canBeTriggeredByNME = true; // le NME peut l'activer ?
            public string triggerId;            // identifiant logique (ex: "OPEN_EXIT_DOOR")
        }
        public List<TriggerPadSpec> triggerPads = new();

        public enum TriggerActionKind
        {
            TimedDoorOpen = 0,
            // (plus tard : ArrowTrapFire, DiscoStart, MessageShow, etc.)
        }

        [System.Serializable]
        public class TriggerBinding
        {
            public string id;                   // doit matcher TriggerPadSpec.triggerId (et/ou un levier si tu veux)
            public TriggerActionKind action;    // pour l’instant : TimedDoorOpen
            public int targetIndex = -1;        // index de la porte (TimedDoorSpec) si action=TimedDoorOpen
            public float secondsOverride = -1f; // <0 => utilise la durée du LevelData.timedDoors[targetIndex]
        }
        public List<TriggerBinding> triggerBindings = new();
    }
}
