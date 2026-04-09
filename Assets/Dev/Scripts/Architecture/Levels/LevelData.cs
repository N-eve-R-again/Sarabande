// FILE: Assets/Dev/Scripts/Levels/LevelData.cs
//
// Rôle du script (résumé)
// - ScriptableObject qui décrit intégralement un puzzle/niveau : dimensions, murs pleins,
//   murs fins (arêtes), spawns Héros/NME, entrée/sortie, décors traversables,
//   spécifications des systèmes (flèches, portes temporisées + leviers, messages,
//   dalles de départ Disco, séquences Disco, grilles/gates, pads de triggers génériques).
// - Sert de "source de vérité" lue par LevelContext.

using Sarabande.Core;
using System;
using System.Collections.Generic;
using UnityEngine;


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

        [Header("Level Parameters")]

        [Min(1)] public int width = 8;     // colonnes (A..H)
        [Min(1)] public int height = 8;    // rangées  (1..8)
        public ExitConfig exit;

        [Header("ObstacleData")]
        public List<ObstacleData> obstacles = new List<ObstacleData>();

        [Header("Listeners")]
        [SerializeReference] public List<ListenerData> listeners = new List<ListenerData>();
        //public List<Vector2Int> fakeWalls = new List<Vector2Int>();

        [Header("Triggerables")]
        [SerializeReference] public List<TriggerableData> triggerables = new List<TriggerableData>();

        [SerializeField] public List<DiscoSequenceConfig> discoSequencesConfigs = new List<DiscoSequenceConfig>();
        // ?????????????????????????????????????????????????????????????????????????????
        // Spawns & entrée/sortie
        // ?????????????????????????????????????????????????????????????????????????????

        [Header("Spawns")]
        public ActorSpawn heroSpawnConfig;

        [System.Obsolete]
        [Header("Legacy Obstacles")]
        [Tooltip("Cases non-walkable (murs pleins). Coordonnées sur la grille (x,z).")]
        [HideInInspector] public List<GridCoord> nonWalkables = new();

        [System.Obsolete]
        [Tooltip("Murs fins entre deux cases adjacentes (arêtes bloquantes).")]
        [HideInInspector] public List<EdgeBlocker> thinWalls = new();

        [Header("Legacy Spawns")]
        [HideInInspector] public GridCoord heroSpawn;  // D8 = (3,7)

        [Tooltip("Liste des cellules de spawn des ennemis. Vide = 0 ennemi.")]
        public List<GridCoord> nmeSpawns = new();

        [Tooltip("Facing par NME (optionnel). Même index que nmeSpawns. Si manquant, on garde initialFacing du prefab.")]
        public List<CardinalDirection> nmeFacings = new();

        [Header("Legacy Entry")]
        [Tooltip("Depuis quel bord le héros arrive pour son entry step.")]
        [HideInInspector]
        public CardinalDirection heroEntry = CardinalDirection.North;

        // Exemple : fromCell = H1, direction = East

        // ?????????????????????????????????????????????????????????????????????????????
        // Décor (visuel traversable)
        // ?????????????????????????????????????????????????????????????????????????????

        [Header("Décor (murs traversables)")]
        [Tooltip("Décor purement visuel : n'arrête ni le Héros/NME ni les flèches, ni la LOS.")]
        [System.Obsolete]
        [HideInInspector] public List<GridCoord> passThroughWalls = new();  // ex: A6, etc.

        // ?????????????????????????????????????????????????????????????????????????????
        // Arrow Traps (proto)
        // ?????????????????????????????????????????????????????????????????????????????


        public void ImportLegacyArrowTraps()
        {

            int i = 0;
            foreach (ArrowTrapSpec item in arrowTraps)
            {

                string key = $"arrowTrap_{i}";
                string[] triggerobjectkey = new string[1]
                {
                    key
                };
                TriggerObjectConfig triggerPad = new TriggerObjectConfig(TriggerObjectType.TriggerPad, triggerobjectkey, item.triggerCell,!item.canRearm,false,1f);
                ArrowTrapConfig arrowTrap = new ArrowTrapConfig(item.startCell,item.travelDir,item.arrowSpeed,item.canRearm,item.rearmDelay, key);
                listeners.Add(triggerPad);
                triggerables.Add(arrowTrap);
                i++;
            }

             
        }
        [ContextMenu("Upgrade All")]
        public void UpgradeLevelData()
        {
            listeners.Clear();
            triggerables.Clear();
            UpdateHeroSpawn();
            ImportLegacyArrowTraps();
            ImportLegacyGates();
            ImportLegacyLevers();
            ImportObstacles();

            ImportOldDisco();
            foreach (var item in messages)
            {
                listeners.Add(item);
            }
            foreach (var item in passThroughWalls)
            {
                listeners.Add(new FakeWallData(item));
            }
        }

        private void ImportOldDisco()
        {
            discoSequencesConfigs.Clear();
            int i = 0;
            foreach (var item in discoStartTiles)
            {
                string key = $"disco{i}";
                string[] triggerobjectkey = new string[1]
                {
                    key
                };
                TriggerObjectConfig triggerPad = new TriggerObjectConfig(TriggerObjectType.TriggerPad, triggerobjectkey, item, false, true, 1f);
                listeners.Add(triggerPad);
            }
            i = 0;
            foreach (var item in discoSequences)
            {

                DiscoSequenceConfig config = new DiscoSequenceConfig("disco" + i,new string[0], new string[0], item.cells,item.stepSeconds,item.defaultStepSeconds);
                discoSequencesConfigs.Add(config);
                i++;
            }
        }
        private void ImportLegacyLevers()
        {

            int i = 0;
            foreach (var item in levers)
            {
                string key = $"door{i}";
                string[] triggerobjectkey = new string[1]
                {
                    key
                };
                Vector2Int newcell = item.cell + GridUtils.DirToVec2(item.requireFacing);
                TriggerObjectConfig temp = new TriggerObjectConfig(TriggerObjectType.Lever, triggerobjectkey, newcell, false, true, 0f, GridUtils.Opposite(item.requireFacing));
                listeners.Add(temp);
                i++;
            }
        }

        private void ImportLegacyGates()
        {

            int i = 0;

            foreach (var item in timedDoors)
            {
                string key = $"door{i}";
                string[] triggerobjectkey = new string[1]
                {
                    key
                };
                
                GateConfig temp = new GateConfig(GateType.Timer, CardinalDirection.West, key, item.cell, item.openSeconds);
                triggerables.Add(temp);
                i++;
            }

            foreach(var item in gridGates)
            {
                string key = $"door{i}";
                string[] triggerobjectkey = new string[1]
                {
                    key
                };

                GateConfig temp = new GateConfig(GateType.OneShot, item.side, key, item.cell, 0f);
                triggerables.Add(temp);
                i++;
            }
        }

        private void UpdateHeroSpawn()
        {
            heroSpawnConfig = new ActorSpawn(heroSpawn,heroEntry);
        }

        private void ImportObstacles()
        {
            obstacles.Clear();
            foreach (var item in nonWalkables)
            {
                obstacles.Add(new ObstacleData(ObstacleData.ObstacleType.Wall, item, CardinalDirection.North));
            }

            foreach (var item in thinWalls)
            {
                obstacles.Add((ObstacleData)item);
            }

        }

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
        [Header("Legacy")]
        [System.Obsolete]
        [Tooltip("Pièges à flèche : quand on marche sur 'triggerCell', une flèche part de 'startCell' dans 'travelDir'.")]
        [HideInInspector] public List<ArrowTrapSpec> arrowTraps = new();

        // ?????????????????????????????????????????????????????????????????????????????
        // Timed Doors & Levers
        // ?????????????????????????????????????????????????????????????????????????????

        [System.Obsolete]
        [System.Serializable]
        public class TimedDoorSpec
        {
            public GridCoord cell;                        // ex: B5
            [Min(0.1f)] public float openSeconds = 3f;    // durée d'ouverture de base
        }
        [HideInInspector] public List<TimedDoorSpec> timedDoors = new();

        [System.Obsolete]
        [System.Serializable]
        public class LeverSpec
        {
            public GridCoord cell;                        // ex: E7
            public CardinalDirection requireFacing = CardinalDirection.North; // direction à pousser
            [Min(0)] public int linkedDoorIndex = 0;     // index dans la liste 'timedDoors'
        }
        [HideInInspector]public List<LeverSpec> levers = new();

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
        [System.Obsolete]
        public class GridGateSpec
        {
            public GridCoord cell;                 // case A
            public CardinalDirection side;             // bord de A (vers B = A + side)
            public bool initiallyOpen = false;     // fermé par défaut (bloque), sinon déjà ouvert
        }
        [System.Obsolete]
        public List<GridGateSpec> gridGates = new();

        // ?????????????????????????????????????????????????????????????????????????????
        // Triggers génériques (Pads + Routage)
        // ?????????????????????????????????????????????????????????????????????????????

        public LevelData Clone()
        {
            // Sérialise en JSON
            string json = JsonUtility.ToJson(this);

            // Crée une nouvelle instance
            LevelData copy = CreateInstance<LevelData>();

            // Désérialise dans la copie
            JsonUtility.FromJsonOverwrite(json, copy);

            return copy;
        }
    }


    [System.Serializable]
    public class DiscoSequenceConfig : TriggerableData
    {
        public string[] successTriggerKeys = new string[0];
        public string[] failTriggerKeys = new string[0];

        [Header("Chemin à fouler (ordre strict)")]
        public List<GridCoord> cells = new List<GridCoord>();

        [Header("Timers par étape (optionnel)")]
        public List<float> stepSeconds = new List<float>(); // si la taille ne match pas, utiliser defaultStepSeconds

        [Header("Fallback timing")]
        public float defaultStepSeconds = 0.8f;

        public DiscoSequenceConfig(string triggerKey,string[] successTriggerKeys, string[] failTriggerKeys , List<GridCoord> cells, List<float> stepSeconds, float defaultStepSeconds)
        {
            this.triggerKey = triggerKey;
            this.successTriggerKeys = successTriggerKeys;
            this.failTriggerKeys = failTriggerKeys;
            this.cells = cells;
            this.stepSeconds = stepSeconds;
            this.defaultStepSeconds = defaultStepSeconds;
        }
        public DiscoSequenceConfig()
        {
            triggerKey = "undefined";
            successTriggerKeys = new string[0];
            failTriggerKeys = new string[0];
            cells = new List<GridCoord>();
            stepSeconds = new List<float>();

        }
    }

    [Serializable]
    public class DiscoDalleData
    {
        Vector2Int cell;
        float stepSecond;
    }

    [System.Serializable]
    public class MessageConfig : ListenerData
    {       
        // coordonnées (utilise x/z comme partout)
        [TextArea(2, 5)] public string text = "text here";
        [Min(0.1f)] public float displaySeconds = 3f;
        public AudioClip voiceClip = null;

        public MessageConfig() : base() { }
    }

    public enum TriggerObjectType
    {
        InvisibleTrigger,
        TriggerPad,
        Lever
    }

    public enum RearmType
    {
        OneShot,
        CallBack,
        Timer,
        Instant
    }

    public enum GateType
    {
        Timer,
        OneShot,
        Toggle
    }

    [System.Serializable]
    public class GateConfig : TriggerableData
    {
        public GateType type;
        public CardinalDirection direction;

        public float timer;
        public bool startopen = false;
        public GateConfig(GateType type, CardinalDirection direction, string triggerKey, Vector2Int cell, float timer)
        {
            this.type = type;
            this.direction = direction;
            this.triggerKey = triggerKey;
            this.cell = cell;
            this.timer = timer;
        }
    }

    [Serializable]
    public class TriggerObjectConfig : ListenerData
    {

        public TriggerObjectType type = TriggerObjectType.TriggerPad;

        [ConditionalHide("type", TriggerObjectType.Lever)]
        public CardinalDirection attachedTo = CardinalDirection.South;

        [Header("Rearm Behaviour")]

        public RearmType rearmType = RearmType.OneShot;
        [Min(0f)] public float timeToRearm = 1f;

        public TriggerObjectConfig(TriggerObjectType type, string[] triggerKeys, Vector2Int cell, bool oneShot, bool waitForCallback, float timeToRearm, CardinalDirection attachedTo = CardinalDirection.North)
        {
            if (oneShot)
            {
                rearmType = RearmType.OneShot;
            }
            else if (waitForCallback)
            {
                rearmType = RearmType.CallBack;
            }
            else
            {
                rearmType = RearmType.Timer;
                this.timeToRearm = timeToRearm;
            }
            this.timeToRearm = 0f;
            this.type = type;
            this.triggerKeys = triggerKeys;
            this.cell = cell;
            this.attachedTo = attachedTo;


        }

        public TriggerObjectConfig() : base()
        {
        }

    }
    [System.Serializable]
    public class FakeWallData : ListenerData
    {
        public FakeWallData(Vector2Int cell) 
        {
            this.cell = cell;
        }

        public FakeWallData() : base()
        {
        }
    }

    [System.Serializable]
    public class DefaultTriggerableData : TriggerableData
    {
        public DefaultTriggerableData(Vector2Int cell, string triggerKey)
        {
            this.cell = cell;
            this.triggerKey = triggerKey;
        }
        public DefaultTriggerableData() : base()
        {
        }
    }


    [System.Serializable]
    public abstract class TriggerableData
    {
        public Vector2Int cell;
        public string triggerKey;
        public TriggerableData() 
        {}
    }

    [System.Serializable]
    public abstract class ListenerData
    {
        public Vector2Int cell = Vector2Int.zero;
        public string[] triggerKeys = new string[0];
        public ListenerData()
        {
        }

    }

    [System.Serializable]
    public class ArrowTrapConfig : TriggerableData
    {
           // première case dans la map que la flèche traverse
        public CardinalDirection travelDir = CardinalDirection.North;         // direction de déplacement (N/E/S/W)
        [Min(0.1f)] public float arrowSpeed = 1f;   // vitesse (unités monde / seconde)

        // --- options de réarmement ---
        public bool canRearm = false;                   // si true, le piège se réarme
        [Min(0f)] public float rearmTimeDelay = 1f;      // délai avant réarmement (secondes)


        public ArrowTrapConfig(GridCoord cell, CardinalDirection travelDir, float arrowSpeed, bool canRearm, float rearmDelay, string triggerKey)
        {
            this.cell = cell;
            this.travelDir = travelDir;
            this.arrowSpeed = arrowSpeed;
            this.canRearm = canRearm;
            this.rearmTimeDelay = rearmDelay;
            this.triggerKey = triggerKey;
        }

        public ArrowTrapConfig() : base()
        {
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
        public string key;
        public string entityType;

        public TriggerKey(string key, string entityType)
        {
            this.key = key;
            this.entityType = entityType;
        }
    }

    [Serializable]
    public class ObstacleData
    {

        public enum ObstacleType
        {
            Wall,
            ThinWall
        }
        public Vector2Int cell;

        public ObstacleType type;

        //[ConditionalHide("type", ObstacleType.ThinWall)]
        public CardinalDirection thinWallDirection = CardinalDirection.North;

        public ObstacleData(ObstacleType type, Vector2Int cell, CardinalDirection thinWallDirection)
        {
            this.type = type;
            this.cell = cell;
            this.thinWallDirection = thinWallDirection;
        }

        public static explicit operator ObstacleData(EdgeBlocker edgeBlocker) {

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
            return new ObstacleData(ObstacleType.ThinWall, cell,dir);
        }


    }

}


