using Sarabande.Core;
using System.Collections.Generic;
using UnityEngine;
using Sarabande.Levels;

namespace Sarabande.Legacy
{


    [System.Obsolete]
    [System.Serializable]
    public struct ArrowTrapSpec
    {
        [Header("Déclencheur & Origine")]
        public GridCoord triggerCell;           // case walkable à fouler (Héros ou NME)
        public GridCoord startCell;             // première case dans la map que la flèche traverse
        public CardinalDirection travelDir;         // direction de déplacement (N/E/S/W)
        [Min(0.1f)] public float arrowSpeed;   // vitesse (unités monde / seconde)

        // --- options de réarmement ---
        public bool canRearm;                   // si true, le piège se réarme
        [Min(0f)] public float rearmDelay;      // délai avant réarmement (secondes)

        // --- Lien facultatif à la Disco ---
        [Header("Disco Link")]
        [Tooltip("Si true : ce trap ne peut s’activer que pendant la Disco, et se réarme à chaque start.")]
        public bool linkToDisco;

        // --- Émissions avancées ---
        [Tooltip("Liste d’émissions ; si vide => fallback legacy (1 flèche).")]
        public List<ArrowEmission> emissions;   // null/empty = comportement historique
    }

    [System.Serializable]
    public class ArrowEmission
    {
        [Header("Origine & Direction")]
        public GridCoord startCell;                          // origine (peut différer de spec.startCell legacy)
        public Sarabande.Core.CardinalDirection travelDir;       // direction de CETTE émission
        [Min(0.05f)] public float arrowSpeed = 6f;

        [Header("Déclenchements fixes (après le trigger)")]
        public List<float> shotTimes;                        // ex: [0, 2, 8] => 3 flèches à 0s, 2s, 8s

        [Header("Pattern répétitif")]
        public bool repeat = false;
        [Min(0f)] public float repeatStartDelay = 0f;
        [Min(0.01f)] public float repeatInterval = 0.2f;

        [Tooltip("Si > 0, répète pendant cette durée (en s). Ignoré si repeatCount > 0.")]
        [Min(0f)] public float repeatDuration = 0f;

        [Tooltip("Si > 0, tire exactement N fois (intervalle constant). Prend le pas sur repeatDuration.")]
        [Min(0)] public int repeatCount = 0;
    }

    [System.Obsolete]
    [System.Serializable]
    public class TimedDoorSpec
    {
        public GridCoord cell;                        // ex: B5
        [Min(0.1f)] public float openSeconds = 3f;    // durée d'ouverture de base
    }

    [System.Obsolete]
    [System.Serializable]
    public class LeverSpec
    {
        public GridCoord cell;                        // ex: E7
        public CardinalDirection requireFacing = CardinalDirection.North; // direction à pousser
        [Min(0)] public int linkedDoorIndex = 0;     // index dans la liste 'timedDoors'
    }

    [System.Obsolete]
    [System.Serializable]
    public class DiscoSequenceSpec
    {
        [Header("Chemin à fouler (ordre strict)")]
        public List<GridCoord> cells = new List<GridCoord>();

        [Header("Timers par étape (optionnel)")]
        public List<float> stepSeconds = new List<float>(); // si la taille ne match pas, utiliser defaultStepSeconds

        [Header("Démarrage automatique")]
        public int startOnDoorIndex = -1; // -1 = pas d’auto-start ; sinon index d’une TimedDoor qui, en s’ouvrant, lance la Disco

        [Header("Fallback timing")]
        public float defaultStepSeconds = 0.8f;
    }

    [System.Serializable]
    [System.Obsolete]
    public class GridGateSpec
    {
        public GridCoord cell;                 // case A
        public CardinalDirection side;             // bord de A (vers B = A + side)
        public bool initiallyOpen = false;     // fermé par défaut (bloque), sinon déjà ouvert
    }
}