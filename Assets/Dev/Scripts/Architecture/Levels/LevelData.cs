// FILE: Assets/Dev/Scripts/Levels/LevelData.cs
//
// Rôle du script (résumé)
// - ScriptableObject qui décrit intégralement un puzzle/niveau : dimensions, murs pleins,
//   murs fins (arêtes), spawns Héros/NME, entrée/sortie, décors traversables,
//   spécifications des systèmes (flèches, portes temporisées + leviers, messages,
//   dalles de départ Disco, séquences Disco, grilles/gates, pads de triggers génériques).
// - Sert de "source de vérité" lue par LevelContext et par tous les systèmes.
//
// Invariants (ne pas casser)
// - Aucun renommage de champs publics/sérialisés, classes internes ou enums.
// - Aucune modification de types, attributs [SerializeField], valeurs par défaut, ni logique.
// - Les listes sont utilisées par index dans d’autres systèmes : l’ordre doit rester géré côté design.
//
// Dépendances
// - Sarabande.Core : GridCoord, EdgeBlocker, EdgeDirection, EdgeExit
// - Utilisé par : HeroController, NMESpawnSystem, GridGateSystem, TimedDoorSystem, LeverSystem,
//                 PressurePadSystem/TriggerRouter, ArrowTrapSystem, DiscoSequenceSystem, MessageSystem, etc.

using Sarabande.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using static Sarabande.Levels.LevelData;

namespace Sarabande.Levels
{
    /// <summary>
    /// Données d'un niveau : dimensions, murs, murs fins, spawns, entrée/sortie, décor traversable,
    /// et specs des systèmes (traps, portes/leviers, triggers, messages, disco, gates).
    /// </summary>
    [CreateAssetMenu(menuName = "Sarabande/Level Data", fileName = "LevelData")]
    public class LevelData : ScriptableObject
    {
        // ?????????????????????????????????????????????????????????????????????????????
        // Grille & obstacles statiques
        // ?????????????????????????????????????????????????????????????????????????????

        [Min(1)] public int width = 8;     // colonnes (A..H)
        [Min(1)] public int height = 8;    // rangées  (1..8)

        [Header("Obstacle")]
        public List<Obstacle> obstacles = new List<Obstacle>();

        [Header("Legacy Obstacles")]
        [Tooltip("Cases non-walkable (murs pleins). Coordonnées sur la grille (x,z).")]
        public List<GridCoord> nonWalkables = new();

        [Tooltip("Murs fins entre deux cases adjacentes (arêtes bloquantes).")]
        public List<EdgeBlocker> thinWalls = new();

        // ?????????????????????????????????????????????????????????????????????????????
        // Spawns & entrée/sortie
        // ?????????????????????????????????????????????????????????????????????????????

        [Header("Spawns")]
        public ActorSpawn heroSpawnConfig;

        [Header("Legacy Spawns")]
        public GridCoord heroSpawn;  // D8 = (3,7)

        [Tooltip("Liste des cellules de spawn des ennemis. Vide = 0 ennemi.")]
        public List<GridCoord> nmeSpawns = new();

        [Tooltip("Facing par NME (optionnel). Même index que nmeSpawns. Si manquant, on garde initialFacing du prefab.")]
        public List<CardinalDirection> nmeFacings = new();

        [Header("Legacy Entry")]
        [Tooltip("Depuis quel bord le héros arrive pour son entry step.")]
        public CardinalDirection heroEntry = CardinalDirection.North;

        [Header("Sortie")]
        public EdgeExit exit;        // Exemple : fromCell = H1, direction = East

        // ?????????????????????????????????????????????????????????????????????????????
        // Décor (visuel traversable)
        // ?????????????????????????????????????????????????????????????????????????????

        [Header("Décor (murs traversables)")]
        [Tooltip("Décor purement visuel : n'arrête ni le Héros/NME ni les flèches, ni la LOS.")]
        public List<GridCoord> passThroughWalls = new();  // ex: A6, etc.

        // ?????????????????????????????????????????????????????????????????????????????
        // Arrow Traps (proto)
        // ?????????????????????????????????????????????????????????????????????????????

        [Header("New Version")]
        public List<ArrowTrapConfig> newArrowTraps = new();
        public List<TriggerPadConfig> newTriggerPads = new();
        public List<TriggerKey> triggerKeys = new();

        [ContextMenu("Upgrade Arrow Traps to New Version")]
        public void ImportLegacyArrowTraps()
        {
            newArrowTraps.Clear();
            newTriggerPads.Clear();
            int i = 0;
            foreach (ArrowTrapSpec item in arrowTraps)
            {
                TriggerPadConfig triggerPad = new TriggerPadConfig(false, item.canRearm, item.triggerCell, i);
                ArrowTrapConfig arrowTrap = new ArrowTrapConfig(item.startCell,item.travelDir,item.arrowSpeed,item.canRearm,item.rearmDelay,i);
                newTriggerPads.Add(triggerPad);
                newArrowTraps.Add(arrowTrap);
                i++;
            }


        }

        private void OnValidate()
        {
            triggerKeys.Clear();
            foreach (var item in newArrowTraps)
            {
                triggerKeys.Add(new TriggerKey(item.triggerKey, $"arrowTrap {item.cell}"));
            }
        }


        [ContextMenu("Upgrade HeroSpawn to New Version")]
        public void UpdateHeroSpawn()
        {
            heroSpawnConfig = new ActorSpawn(heroSpawn,heroEntry);
        }

        [ContextMenu("Upgrade Obstacles to New Version")]
        public void UpdateObstacles()
        {
            obstacles.Clear();
            foreach (var item in nonWalkables)
            {
                obstacles.Add(new Obstacle(Obstacle.ObstacleType.Wall, item, CardinalDirection.North));
            }

            foreach (var item in thinWalls)
            {
                obstacles.Add((Obstacle)item);
            }


        }


        [ContextMenu("Upgrade ALL LEVEL DATA to New Version")]
        public void PortLevelDataToNewVersion()
        {
            UpdateHeroSpawn();
            ImportLegacyArrowTraps();
        }

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
        [Header("Legacy")]
        [Tooltip("Pièges à flèche : quand on marche sur 'triggerCell', une flèche part de 'startCell' dans 'travelDir'.")]
        public List<ArrowTrapSpec> arrowTraps = new();

        // ?????????????????????????????????????????????????????????????????????????????
        // Timed Doors & Levers
        // ?????????????????????????????????????????????????????????????????????????????

        [System.Serializable]
        public class TimedDoorSpec
        {
            public GridCoord cell;                        // ex: B5
            [Min(0.1f)] public float openSeconds = 3f;    // durée d'ouverture de base
        }
        public List<TimedDoorSpec> timedDoors = new();


        [System.Serializable]
        public class LeverSpec
        {
            public GridCoord cell;                        // ex: E7
            public CardinalDirection requireFacing = CardinalDirection.North; // direction à pousser
            [Min(0)] public int linkedDoorIndex = 0;     // index dans la liste 'timedDoors'
        }
        public List<LeverSpec> levers = new();

        // ?????????????????????????????????????????????????????????????????????????????
        // Messages
        // ?????????????????????????????????????????????????????????????????????????????

        [Header("Messages")]
        public List<MessageConfig> messages = new();


        // ?????????????????????????????????????????????????????????????????????????????
        // Dalles Disco & Séquences
        // ?????????????????????????????????????????????????????????????????????????????

        [Header("Disco Start")]
        [Tooltip("Cases qui déclenchent la séquence Disco quand le Héros y entre.")]
        public List<GridCoord> discoStartTiles = new();

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

        public List<DiscoSequenceSpec> discoSequences = new List<DiscoSequenceSpec>();

        // ?????????????????????????????????????????????????????????????????????????????
        // Grid Gates (barreaux sur une ARÊTE -> bloquent le passage, pas la LOS/flèches)
        // ?????????????????????????????????????????????????????????????????????????????

        [System.Serializable]
        public class GridGateSpec
        {
            public GridCoord cell;                 // case A
            public CardinalDirection side;             // bord de A (vers B = A + side)
            public bool initiallyOpen = false;     // fermé par défaut (bloque), sinon déjà ouvert
        }
        public List<GridGateSpec> gridGates = new();

        // ?????????????????????????????????????????????????????????????????????????????
        // Triggers génériques (Pads + Routage)
        // ?????????????????????????????????????????????????????????????????????????????




    }

    [System.Serializable]
    public class MessageConfig
    {
        public Vector2Int cell;        // coordonnées (utilise x/z comme partout)
        [TextArea(2, 5)] public string text;
        [Min(0.1f)] public float displaySeconds = 3f;
        public AudioClip voiceClip;
    }

    [Serializable]
    public class LeverConfig
    {
        public bool oneShot = true;
        [Tooltip("si -1 alors attendra le callback du triggerable")][Min(-1f)] public float timeToRearm = 1f;

        public CardinalDirection attachedToSide = CardinalDirection.North;
        public GridCoord cell;
        public int triggerKey = -1;
        ListenerInteractionLayer interactionLayer = new ListenerInteractionLayer(true, false);

        public LeverConfig(bool _oneShot, GridCoord _cell, int _triggerKey)
        {
            oneShot = _oneShot;
            cell = _cell;
            triggerKey = _triggerKey;
               
        }
    }
    

    [System.Serializable]
    public class TriggerPadConfig
    {
        public bool invisible = false;
        public bool oneShot = true;

        [Tooltip("si -1 alors attendra le callback du triggerable")] [Min(-1f)] public float timeToRearm = 1f;
        public Vector2Int cell;
        public int triggerKey = -1;

        ListenerInteractionLayer interactionLayer = new ListenerInteractionLayer(true, true);

        public TriggerPadConfig(bool _invisible, bool _oneShot, GridCoord _cell, int _triggerKey)
        {
            invisible = _invisible;
            oneShot = _oneShot;
            cell = _cell;
            triggerKey = _triggerKey;
        }
    }


    [System.Serializable]
    public class ArrowTrapConfig
    {
        public Vector2Int cell;             // première case dans la map que la flèche traverse
        public CardinalDirection travelDir;         // direction de déplacement (N/E/S/W)
        [Min(0.1f)] public float arrowSpeed;   // vitesse (unités monde / seconde)

        // --- options de réarmement ---
        public bool canRearm;                   // si true, le piège se réarme
        [Min(0f)] public float rearmTimeDelay = 1f;      // délai avant réarmement (secondes)

        public int triggerKey = -1;

        public ArrowTrapConfig(GridCoord cell, CardinalDirection travelDir, float arrowSpeed, bool canRearm, float rearmDelay, int triggerKey)
        {
            this.cell = cell;
            this.travelDir = travelDir;
            this.arrowSpeed = arrowSpeed;
            this.canRearm = canRearm;
            this.rearmTimeDelay = rearmDelay;
            this.triggerKey = triggerKey;
        }

        
    }

    [System.Serializable]
    public class ActorSpawn
    {
        public Vector2Int spawnCell;
        public CardinalDirection spawnDirection;

        public ActorSpawn(GridCoord spawnCell, CardinalDirection spawnDirection)
        {
            this.spawnCell = spawnCell;
            this.spawnDirection = spawnDirection;
        }
    }

    [System.Serializable]
    public class TriggerKey
    {
        public int key;
        public string entityType;

        public TriggerKey(int key, string entityType)
        {
            this.key = key;
            this.entityType = entityType;
        }
    }

    [Serializable]
    public class Obstacle
    {
        public enum ObstacleType
        {
            Wall,
            ThinWall
        }

        public ObstacleType type;
        public Vector2Int cell;

        [Space]
        [ConditionalHide("type",ObstacleType.ThinWall, "ThinWall Config")]
        public CardinalDirection direction;

        public Obstacle(ObstacleType type, Vector2Int cell, CardinalDirection direction)
        {
            this.type = type;
            this.cell = cell;
            this.direction = direction;
        }

        public static explicit operator Obstacle(EdgeBlocker edgeBlocker) {

            CardinalDirection dir = CardinalDirection.North;
            Vector2Int cell = edgeBlocker.a;
            if (edgeBlocker.a.x == edgeBlocker.b.x) {
                dir = CardinalDirection.North;
                cell = Vector2Int.Min(edgeBlocker.a, edgeBlocker.b);
            }
            if (edgeBlocker.a.z == edgeBlocker.b.z) {
                dir = CardinalDirection.East;
                cell = Vector2Int.Min(edgeBlocker.a, edgeBlocker.b);
            }


            return new Obstacle(ObstacleType.ThinWall, cell,dir);
        }


    }
}


