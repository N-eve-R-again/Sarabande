// FILE: Assets/DEV/Scripts/Triggers/PressurePadSystem.cs
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

        // runtime
        private NMEController[] _nmes;

        private class PadRuntime
        {
            public Vector2Int cell;
            public string triggerId;
            public bool allowNME;
            public bool armed = true;

            public TrapTileVisual visual;

            // fermeture attendue : combien de DoorClosed avant réarmement
            public HashSet<int> doorTargets = new();
            public int waitingClosures = 0;
        }

        private readonly Dictionary<Vector2Int, int> _padIndexByCell = new();
        private readonly List<PadRuntime> _pads = new();

        private Vector2Int _lastHeroCell;
        private readonly Dictionary<NMEController, Vector2Int> _lastNmeCell = new();

        private void Awake()
        {
            if (!hero) hero = FindFirstObjectByType<HeroController>(FindObjectsInactive.Include);
            if (!timedDoorSystem) timedDoorSystem = FindFirstObjectByType<TimedDoorSystem>(FindObjectsInactive.Include);
            if (!triggerRouter) triggerRouter = FindFirstObjectByType<TriggerRouter>(FindObjectsInactive.Include);

            _nmes = FindObjectsByType<NMEController>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            BuildFromLevelData();
        }

        private void BuildFromLevelData()
        {
            _pads.Clear();
            _padIndexByCell.Clear();

            if (levelData == null || levelData.triggerPads == null) return;

            // dictionnaire des visuals présents
            var visMap = new Dictionary<Vector2Int, TrapTileVisual>();
            if (padVisualsInScene != null && padVisualsInScene.Length > 0)
            {
                foreach (var v in padVisualsInScene) if (v) visMap[v.Cell] = v;
            }
            else
            {
                var found = FindObjectsByType<TrapTileVisual>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var v in found) if (v) visMap[v.Cell] = v;
            }

            for (int i = 0; i < levelData.triggerPads.Count; i++)
            {
                var spec = levelData.triggerPads[i];
                var cell = new Vector2Int(spec.cell.x, spec.cell.z);

                if (!visMap.TryGetValue(cell, out var tv) || tv == null)
                {
                    tv = CreatePadVisual(cell);
                    if (tv) visMap[cell] = tv;
                }

                var r = new PadRuntime
                {
                    cell = cell,
                    triggerId = spec.triggerId,
                    allowNME = spec.canBeTriggeredByNME,
                    armed = true,
                    visual = tv
                };

                // Liste des portes ciblées par ce trigger (pour la remontée)
                if (triggerRouter != null)
                {
                    var targets = triggerRouter.GetTimedDoorTargetsForId(r.triggerId);
                    foreach (var t in targets) r.doorTargets.Add(t);
                }

                _padIndexByCell[cell] = _pads.Count;
                _pads.Add(r);
            }

            // init mémoires de cellule
            if (hero) _lastHeroCell = hero.GridPos;
            if (_nmes != null)
            {
                _lastNmeCell.Clear();
                foreach (var n in _nmes) if (n) _lastNmeCell[n] = n.GridPos;
            }
        }

        private void Update()
        {
            if (hero)
            {
                var hc = hero.GridPos;
                if (hc != _lastHeroCell)
                {
                    TryTriggerAtCell(hc, isHero: true);
                    _lastHeroCell = hc;
                }
            }

            if (_nmes != null)
            {
                foreach (var n in _nmes)
                {
                    if (!n) continue;
                    var c = n.GridPos;
                    if (_lastNmeCell.TryGetValue(n, out var prev))
                    {
                        if (c != prev)
                        {
                            TryTriggerAtCell(c, isHero: false);
                            _lastNmeCell[n] = c;
                        }
                    }
                    else
                    {
                        _lastNmeCell[n] = c;
                    }
                }
            }
        }

        private void TryTriggerAtCell(Vector2Int cell, bool isHero)
        {
            if (!_padIndexByCell.TryGetValue(cell, out int idx)) return;
            var p = _pads[idx];
            if (!p.armed) return;
            if (!isHero && !p.allowNME) return;

            // Déclenchement
            if (!string.IsNullOrEmpty(p.triggerId) && triggerRouter != null)
                triggerRouter.Fire(p.triggerId);

            Sarabande.Core.NoiseSystem.Emit(cell);

            // Visuel : s’enfonce et reste enfoncée
            if (p.visual) p.visual.Press();

            // Armement : rester bloqué tant que les portes cibles ne se sont pas refermées
            if (p.doorTargets.Count > 0)
            {
                p.waitingClosures = p.doorTargets.Count;
                p.armed = false;
            }
            else
            {
                // Pas de porte liée ? on réarme tout de suite (petite remontée immédiate)
                p.armed = true;
                if (p.visual) p.visual.Release();
            }
        }

        private void OnAnyDoorClosed(int doorIndex)
        {
            // décrémente les pads en attente qui suivent cette porte
            for (int i = 0; i < _pads.Count; i++)
            {
                var p = _pads[i];
                if (p.armed) continue;                 // pas en attente
                if (p.waitingClosures <= 0) continue;  // rien à attendre
                if (!p.doorTargets.Contains(doorIndex)) continue;

                p.waitingClosures = Mathf.Max(0, p.waitingClosures - 1);

                if (p.waitingClosures == 0)
                {
                    // tout est refermé -> on relâche la dalle et on réarme
                    if (p.visual) p.visual.Release();
                    p.armed = true;
                }
            }
        }

        // --- IResettable ---
        public void ResetToInitial()
        {
            foreach (var p in _pads)
            {
                p.armed = true;
                p.waitingClosures = 0;
                if (p.visual) p.visual.Release();
            }
            if (hero) _lastHeroCell = hero.GridPos;
            if (_nmes != null)
            {
                _lastNmeCell.Clear();
                foreach (var n in _nmes) if (n) _lastNmeCell[n] = n.GridPos;
            }
        }

        // --- LevelContext plumbing ---
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
        private TrapTileVisual CreatePadVisual(Vector2Int cell)
        {
            // Géométrie identique aux dalles de pièges (unifiée)
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = $"Pad_{cell.x}_{cell.y}";
            go.transform.SetParent(transform, false);

            // Centre de la case
            var center = Center(cell, cellSize);

            // Dimensions (comme ArrowTrapVisuals)
            float sx = padTileSizeScale * cellSize;
            float sy = padTileThickness;
            float sz = padTileSizeScale * cellSize;

            // Centres Y (repos/enfoncé) -> on centre le cube et on anime le "centre", pas la face
            float yDownCenter = sy * 0.5f;                  // affleure le sol
            float yUpCenter = yDownCenter + padTileUpLift; // léger relief

            // Pose au repos (léger relief)
            go.transform.position = new Vector3(center.x, yUpCenter, center.z);
            go.transform.localScale = new Vector3(sx, sy, sz);

            // Mat/ombres + pas de collider
            var col = go.GetComponent<Collider>(); if (col) Destroy(col);
            var mr = go.GetComponent<MeshRenderer>();
            if (mr)
            {
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                if (padTileMaterial) mr.sharedMaterial = padTileMaterial;
            }

            // Animation via TrapTileVisual (mêmes durées que les dalles pièges)
            var tv = go.AddComponent<TrapTileVisual>();
            tv.SetupCell(cell);
            tv.ConfigureHeights(yUpCenter, yDownCenter);
            tv.ConfigureDurations(padTilePressDuration, padTileReleaseDuration);
            return tv;
        }

        private void DetachContext()
        {
            if (levelContext != null)
                levelContext.LevelDataChanged -= HandleContextLevelDataChanged;
        }
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
            if (timedDoorSystem) timedDoorSystem.DoorClosed += OnAnyDoorClosed;
        }
        private void OnDisable()
        {
            if (timedDoorSystem) timedDoorSystem.DoorClosed -= OnAnyDoorClosed;
            DetachContext();
        }
#if UNITY_EDITOR
private void OnValidate() { if (!Application.isPlaying) AttachContext(); }
#endif
    }
}

