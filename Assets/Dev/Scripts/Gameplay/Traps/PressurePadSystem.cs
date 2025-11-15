// FILE: Assets/DEV/Scripts/Triggers/PressurePadSystem.cs
//
// Rôle (résumé)
// - Gère les dalles de pression ("pads") définies dans LevelData.triggerPads.
// - Déclenche un TriggerRouter.Fire(triggerId) quand Héros/NME entre sur la cellule du pad.
// - Optionnellement, attend la fermeture d’un ensemble de portes cibles (TimedDoorSystem) pour se réarmer.
// - Construit/associe un visuel unifié (TrapTileVisual) par pad ; animation Press/Release.
// - Implémente IResettable : réarme tous les pads et remet les visuels.
//
// Invariants
// - AUCUN renommage de champs sérialisés / méthodes publiques / signatures existantes.
// - Aucune modification de logique ; uniquement commentaires et renommage de variables **locales** plus explicites.
// - Reste compatible avec HeroController / NMEController / TimedDoorSystem / TriggerRouter existants.

using System.Collections.Generic;
using UnityEngine;
using Sarabande.Levels;
using Sarabande.Core;
using Sarabande.Player;
using Sarabande.NME;
using Sarabande.Doors;
using Sarabande.Traps;
using static Sarabande.Core.GridUtils;

namespace Sarabande.Triggers
{
    public class PressurePadSystem : MonoBehaviour, IResettable
    {
        // ?????????????????????????????????????????????????????????????????????????????
        // Context & Data (noms conservés)
        // ?????????????????????????????????????????????????????????????????????????????

        [Header("Context & Data")]
        [SerializeField] private bool useLevelContext = true;
        [SerializeField] private Sarabande.Core.LevelContext levelContext;
        [SerializeField, HideInInspector] private LevelData levelData;
        [SerializeField, Min(0.001f)] private float cellSize = 1f;

        [Header("Refs")]
        [SerializeField] private HeroController hero;
        [SerializeField] private TimedDoorSystem timedDoorSystem;
        [SerializeField] private TriggerRouter triggerRouter;

        [Header("Visuals")]
        [Tooltip("Optionnel : si laissé vide, on fera un FindObjectsByType<TrapTileVisual>().")]
        [SerializeField] private TrapTileVisual[] padVisualsInScene;

        [Header("Pad Tile (visuel unifié)")]
        [SerializeField] private Material padTileMaterial;
        [SerializeField, Range(0.5f, 0.98f)] private float padTileSizeScale = 0.88f;
        [SerializeField, Min(0.005f)] private float padTileThickness = 0.02f;
        [SerializeField, Min(0f)] private float padTileUpLift = 0.01f;
        [SerializeField, Min(0.01f)] private float padTilePressDuration = 0.06f;
        [SerializeField, Min(0.01f)] private float padTileReleaseDuration = 0.15f;

        // ?????????????????????????????????????????????????????????????????????????????
        // Runtime (noms conservés)
        // ?????????????????????????????????????????????????????????????????????????????

        private NMEController[] _nmes;

        private class PadRuntime
        {
            public Vector2Int cell;
            public string triggerId;
            public bool allowNME;
            public bool armed = true;

            public TrapTileVisual visual;

            // Fermeture attendue : combien de DoorClosed avant réarmement
            public HashSet<int> doorTargets = new();
            public int waitingClosures = 0;
        }

        private readonly Dictionary<Vector2Int, int> _padIndexByCell = new();
        private readonly List<PadRuntime> _pads = new();

        private Vector2Int _lastHeroCell;
        private readonly Dictionary<NMEController, Vector2Int> _lastNmeCell = new();

        // ?????????????????????????????????????????????????????????????????????????????
        // Unity lifecycle
        // ?????????????????????????????????????????????????????????????????????????????

        /// <summary>
        /// Récupère les refs manquantes, met en cache les NME, puis construit à partir du LevelData.
        /// </summary>
        private void Awake()
        {
            if (!hero) hero = FindFirstObjectByType<HeroController>(FindObjectsInactive.Include);
            if (!timedDoorSystem) timedDoorSystem = FindFirstObjectByType<TimedDoorSystem>(FindObjectsInactive.Include);
            if (!triggerRouter) triggerRouter = FindFirstObjectByType<TriggerRouter>(FindObjectsInactive.Include);

            _nmes = FindObjectsByType<NMEController>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            BuildFromLevelData();
        }

        /// <summary>
        /// Première mise en cache sûre côté NME (si des rebuilds surviennent au spawn).
        /// </summary>
        private void Start()
        {
            RefreshNMECache(); // nouveau : fait un 1er cache safe
        }

        // ?????????????????????????????????????????????????????????????????????????????
        // Build
        // ?????????????????????????????????????????????????????????????????????????????

        /// <summary>
        /// Construit la table des pads et leur visuel, initialise les mémoires de cellule.
        /// </summary>
        private void BuildFromLevelData()
        {
            _pads.Clear();
            _padIndexByCell.Clear();

            if (levelData == null || levelData.triggerPads == null) return;

            // Dictionnaire des visuals présents dans la scène (si fournis)
            var visualByCell = new Dictionary<Vector2Int, TrapTileVisual>();
            if (padVisualsInScene != null && padVisualsInScene.Length > 0)
            {
                foreach (var tv in padVisualsInScene) if (tv) visualByCell[tv.Cell] = tv;
            }
            else
            {
                var found = FindObjectsByType<TrapTileVisual>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var tv in found) if (tv) visualByCell[tv.Cell] = tv;
            }

            for (int i = 0; i < levelData.triggerPads.Count; i++)
            {
                var spec = levelData.triggerPads[i];
                var cell = new Vector2Int(spec.cell.x, spec.cell.z);

                if (!visualByCell.TryGetValue(cell, out var trapTileVisual) || trapTileVisual == null)
                {
                    trapTileVisual = CreatePadVisual(cell);
                    if (trapTileVisual) visualByCell[cell] = trapTileVisual;
                }

                var padRuntime = new PadRuntime
                {
                    cell = cell,
                    triggerId = spec.triggerId,
                    allowNME = spec.canBeTriggeredByNME,
                    armed = true,
                    visual = trapTileVisual
                };

                // Liste des portes ciblées par ce trigger (pour la remontée)
                if (triggerRouter != null)
                {
                    var targets = triggerRouter.GetTimedDoorTargetsForId(padRuntime.triggerId);
                    foreach (var doorIndex in targets) padRuntime.doorTargets.Add(doorIndex);
                }

                _padIndexByCell[cell] = _pads.Count;
                _pads.Add(padRuntime);
            }

            // Init mémoires de cellule
            if (hero) _lastHeroCell = hero.GridPos;
            if (_nmes != null)
            {
                _lastNmeCell.Clear();
                foreach (var n in _nmes) if (n) _lastNmeCell[n] = n.GridPos;
            }
        }

        // ?????????????????????????????????????????????????????????????????????????????
        // Gameplay loop
        // ?????????????????????????????????????????????????????????????????????????????

        /// <summary>
        /// Surveille les entrées de cellule pour Héros et NME ; déclenche les pads à l’arrivée.
        /// </summary>
        private void Update()
        {
            if (hero)
            {
                var currentHeroCell = hero.GridPos;
                if (currentHeroCell != _lastHeroCell)
                {
                    TryTriggerAtCell(currentHeroCell, isHero: true);
                    _lastHeroCell = currentHeroCell;
                }
            }

            if (_nmes != null)
            {
                foreach (var n in _nmes)
                {
                    if (!n) continue;
                    var currentNmeCell = n.GridPos;
                    if (_lastNmeCell.TryGetValue(n, out var previousCell))
                    {
                        if (currentNmeCell != previousCell)
                        {
                            TryTriggerAtCell(currentNmeCell, isHero: false);
                            _lastNmeCell[n] = currentNmeCell;
                        }
                    }
                    else
                    {
                        _lastNmeCell[n] = currentNmeCell;
                    }
                }
            }
        }

        /// <summary>
        /// Tente de déclencher un pad si présent à <paramref name="cell"/> et si armé.
        /// Respecte allowNME et gère le (ré)armement selon les portes cibles.
        /// </summary>
        private void TryTriggerAtCell(Vector2Int cell, bool isHero)
        {
            if (!_padIndexByCell.TryGetValue(cell, out int padIndex)) return;
            var pad = _pads[padIndex];
            if (!pad.armed) return;
            if (!isHero && !pad.allowNME) return;

            // Déclenchement logique
            if (!string.IsNullOrEmpty(pad.triggerId) && triggerRouter != null)
                triggerRouter.Fire(pad.triggerId);

            Sarabande.Core.NoiseSystem.Emit(cell);

            // Visuel : s’enfonce et reste enfoncée
            if (pad.visual) pad.visual.Press();

            // Armement : rester bloqué tant que les portes cibles ne se sont pas refermées
            if (pad.doorTargets.Count > 0)
            {
                pad.waitingClosures = pad.doorTargets.Count;
                pad.armed = false;
            }
            else
            {
                // Pas de porte liée ? on réarme tout de suite (petite remontée immédiate)
                pad.armed = true;
                if (pad.visual) pad.visual.Release();
            }
        }

        /// <summary>
        /// Décrémentation des pads en attente quand une porte cible se referme.
        /// Quand le compte atteint 0, on relâche le visuel et on réarme le pad.
        /// </summary>
        private void OnAnyDoorClosed(int doorIndex)
        {
            for (int i = 0; i < _pads.Count; i++)
            {
                var pad = _pads[i];
                if (pad.armed) continue;                 // pas en attente
                if (pad.waitingClosures <= 0) continue;  // rien à attendre
                if (!pad.doorTargets.Contains(doorIndex)) continue;

                pad.waitingClosures = Mathf.Max(0, pad.waitingClosures - 1);

                if (pad.waitingClosures == 0)
                {
                    // tout est refermé -> on relâche la dalle et on réarme
                    if (pad.visual) pad.visual.Release();
                    pad.armed = true;
                }
            }
        }

        // ?????????????????????????????????????????????????????????????????????????????
        // IResettable
        // ?????????????????????????????????????????????????????????????????????????????

        /// <summary>
        /// Réarme tous les pads, coupe les attentes, remet les visuels en Release
        /// et resynchronise les mémoires de cellule Héros/NME.
        /// </summary>
        public void ResetToInitial()
        {
            foreach (var pad in _pads)
            {
                pad.armed = true;
                pad.waitingClosures = 0;
                if (pad.visual) pad.visual.Release();
            }
            if (hero) _lastHeroCell = hero.GridPos;
            if (_nmes != null)
            {
                _lastNmeCell.Clear();
                foreach (var n in _nmes) if (n) _lastNmeCell[n] = n.GridPos;
            }
        }

        // ?????????????????????????????????????????????????????????????????????????????
        // LevelContext plumbing
        // ?????????????????????????????????????????????????????????????????????????????

        /// <summary>
        /// Abonnement au LevelContext (si activé) pour suivre les changements de LevelData.
        /// </summary>
        private void AttachContext()
        {
            if (!useLevelContext) return;
            if (!levelContext)
                levelContext = GetComponentInParent<Sarabande.Core.LevelContext>();

            if (levelContext != null)
            {
                levelContext.LevelDataChanged += HandleContextLevelDataChanged;
                HandleContextLevelDataChanged(levelContext.LevelData);
            }
            else
            {
                Debug.LogWarning($"[{GetType().Name}] Aucun LevelContext parent trouvé.");
            }
        }

        /// <summary>
        /// Crée (si nécessaire) un visuel de dalle standardisé et renvoie son TrapTileVisual.
        /// </summary>
        private TrapTileVisual CreatePadVisual(Vector2Int cell)
        {
            // Géométrie identique aux dalles de pièges (unifiée)
            var padGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
            padGO.name = $"Pad_{cell.x}_{cell.y}";
            padGO.transform.SetParent(transform, false);

            // Centre de la case
            var cellCenterWorld = Center(cell, cellSize);

            // Dimensions (comme ArrowTrapVisuals)
            float sizeX = padTileSizeScale * cellSize;
            float sizeY = padTileThickness;
            float sizeZ = padTileSizeScale * cellSize;

            // Centres Y (repos/enfoncé) -> on centre le cube et on anime le "centre", pas la face
            float yDownCenter = sizeY * 0.5f;                  // affleure le sol
            float yUpCenter = yDownCenter + padTileUpLift;     // léger relief

            // Pose au repos (léger relief)
            padGO.transform.position = new Vector3(cellCenterWorld.x, yUpCenter, cellCenterWorld.z);
            padGO.transform.localScale = new Vector3(sizeX, sizeY, sizeZ);

            // Mat/ombres + pas de collider
            var colliderComponent = padGO.GetComponent<Collider>(); if (colliderComponent) Destroy(colliderComponent);
            var meshRenderer = padGO.GetComponent<MeshRenderer>();
            if (meshRenderer)
            {
                meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                meshRenderer.receiveShadows = false;
                if (padTileMaterial) meshRenderer.sharedMaterial = padTileMaterial;
            }

            // Animation via TrapTileVisual (mêmes durées que les dalles pièges)
            var trapTileVisual = padGO.AddComponent<TrapTileVisual>();
            trapTileVisual.SetupCell(cell);
            trapTileVisual.ConfigureHeights(yUpCenter, yDownCenter);
            trapTileVisual.ConfigureDurations(padTilePressDuration, padTileReleaseDuration);
            return trapTileVisual;
        }

        /// <summary>
        /// Rafraîchit le cache des NME (à appeler après respawn/rebuild).
        /// </summary>
        public void RefreshNMECache()
        {
            _nmes = FindObjectsByType<NMEController>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            // Optionnel: réinitialiser le suivi de position par NME si tu l’utilises
            _lastNmeCell.Clear();
            if (_nmes != null)
                foreach (var n in _nmes) if (n) _lastNmeCell[n] = n.GridPos;
        }

        /// <summary>Se désabonne du LevelContext.</summary>
        private void DetachContext()
        {
            if (levelContext != null)
                levelContext.LevelDataChanged -= HandleContextLevelDataChanged;
        }

        /// <summary>
        /// Callback lors d’un changement de LevelData : reconstruit depuis les données.
        /// </summary>
        private void HandleContextLevelDataChanged(LevelData ld)
        {
            if (levelData == ld) return;
            levelData = ld;
#if UNITY_EDITOR
            if (!Application.isPlaying)
                UnityEditor.EditorUtility.SetDirty(this);
#endif
            BuildFromLevelData();
        }

        private void OnEnable()
        {
            AttachContext();
            Sarabande.NME.NMESpawnSystem.AfterRebuild += RefreshNMECache;
            if (timedDoorSystem) timedDoorSystem.DoorClosed += OnAnyDoorClosed;
        }

        private void OnDisable()
        {
            if (timedDoorSystem) timedDoorSystem.DoorClosed -= OnAnyDoorClosed;
            Sarabande.NME.NMESpawnSystem.AfterRebuild -= RefreshNMECache;
            DetachContext();
        }

#if UNITY_EDITOR
        private void OnValidate() { if (!Application.isPlaying) AttachContext(); }
#endif
    }
}
