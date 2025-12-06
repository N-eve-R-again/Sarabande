// FILE: Assets/DEV/Scripts/Disco/DiscoStartTileSystem.cs
using System.Collections.Generic;
using UnityEngine;
using Sarabande.Core;
using Sarabande.Levels;
using Sarabande.Player;
using static Sarabande.Core.GridUtils;

namespace Sarabande.Disco
{
    /// <summary>
    /// Dalles "DISCO" qui déclenchent une séquence quand le HÉRO entre dessus.
    /// - La dalle qui lance la séquence reste APPUYÉE pendant toute la séquence.
    /// - Si la séquence est LOUPEE : la dalle se RELÈVE et redevient actionnable.
    /// - Si la séquence est RÉUSSIE : la dalle reste APPUYÉE définitivement.
    /// - Visuels: Ready (activer) / Active (appuyée).
    /// </summary>
    public class DiscoStartTileSystem : MonoBehaviour, IResettable
    {
        [Header("Data & Refs")]
        [SerializeField] private HeroController hero;
        [SerializeField] private DiscoSequenceSystem disco;   // référence au système de séquence
        [SerializeField, Min(0.001f)] private float cellSize = 1f;
        [SerializeField] private int sequenceIndex = 0;
        [SerializeField] private bool useLevelContext = true;
        [SerializeField] private Sarabande.Core.LevelContext levelContext;
        [SerializeField, HideInInspector] private Sarabande.Levels.LevelData levelData;


        [Header("Visuals")]
        [SerializeField] private Material readyMaterial;      // visuel "dispo"
        [SerializeField] private Material activeMaterial;     // visuel "appuyée"
        [SerializeField, Range(0.4f, 0.98f)] private float tileSizeScale = 0.9f;
        [SerializeField, Min(0.002f)] private float tileThickness = 0.03f;
        [SerializeField, Min(0f)] private float liftUp = 0.02f;    // centre Y en position relevée
        [SerializeField, Min(0f)] private float liftDown = 0.0f;   // centre Y en position appuyée (0 => affleure le sol)

        private struct StartTile
        {
            public GameObject go;
            public Renderer rend;
            public float sy;       // épaisseur (hauteur monde)
            public float yUp;      // centre Y relevé
            public float yDown;    // centre Y appuyé
        }

        private readonly HashSet<Vector2Int> _starts = new();
        private readonly Dictionary<Vector2Int, StartTile> _tiles = new();

        private Vector2Int _lastHeroCell;
        private bool _isRunning = false;
        private bool _sequenceSucceeded = false;
        private bool _hasActive = false;
        private Vector2Int _activeCell;

        private Transform _parent;

        private void Awake()
        {
            if (!levelData || !hero || !disco)
            {
                Debug.LogError("[DiscoStartTileSystem] Références manquantes (LevelData/Hero/Disco).");
                enabled = false; return;
            }
            levelData = levelContext.LevelData;
            // Abonnements aux événements de la séquence
            disco.onSequenceSuccess.AddListener(OnSequenceSuccess);
            disco.onSequenceFail.AddListener(OnSequenceFail);

            _parent = new GameObject("DiscoStartTiles").transform;
            _parent.SetParent(transform, false);

            BuildTiles();
            _lastHeroCell = hero.GridPos;

            // État initial : aucune séquence en cours
            SetAllReady();
        }

        private void OnDestroy()
        {
            if (disco)
            {
                disco.onSequenceSuccess.RemoveListener(OnSequenceSuccess);
                disco.onSequenceFail.RemoveListener(OnSequenceFail);
            }
        }

        private void Update()
        {
            var cell = hero.GridPos;
            if (cell == _lastHeroCell) return;
            _lastHeroCell = cell;

            // Pas de re-déclenchement pendant qu'une séquence tourne, ni après succès.
            if (_isRunning || _sequenceSucceeded) return;

            // Si on marche sur une dalle start "dispo" => déclenche
            if (_starts.Contains(cell))
            {
                TriggerFrom(cell);
            }
        }

        private void TriggerFrom(Vector2Int cell)
        {
            if (!_tiles.TryGetValue(cell, out var t)) return;

            // Appuie visuellement la dalle (et change le material)
            SetPressed(t, pressed: true);

            _activeCell = cell;
            _hasActive = true;

            _isRunning = true;
            _sequenceSucceeded = false;

            disco.StartSequence(sequenceIndex);
        }

        private void OnSequenceFail()
        {
            _isRunning = false;

            // On relève la dalle active (si on en avait une)
            if (_hasActive && _tiles.TryGetValue(_activeCell, out var t))
                SetPressed(t, pressed: false);

            _hasActive = false;
        }

        private void OnSequenceSuccess()
        {
            _isRunning = false;
            _sequenceSucceeded = true;

            // On laisse la dalle active appuyée (ne rien faire).
            // S'il n'y en avait pas (peu probable), on ne touche à rien.
        }

        // --- Build & visuals ----------------------------------------------------

        private void BuildTiles()
        {
            _starts.Clear();
            _tiles.Clear();

            if (levelData.discoStartTiles == null || levelData.discoStartTiles.Count == 0)
            {
                Debug.LogWarning("[DiscoStartTileSystem] Aucune dalle disco configurée dans LevelData.discoStartTiles.");
                return;
            }

            foreach (var gc in levelData.discoStartTiles)
            {
                var cell = new Vector2Int(gc.x, gc.z);
                if (!_starts.Add(cell)) continue;

                var center = Center(cell, cellSize);

                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = $"DiscoStart_{cell.x}_{cell.y}";
                go.transform.SetParent(_parent, false);

                float sx = tileSizeScale * cellSize;
                float sy = tileThickness;
                float sz = tileSizeScale * cellSize;

                // positions Y (centres)
                float yUp = liftUp + sy * 0.5f;
                float yDown = liftDown + sy * 0.5f;

                go.transform.position = new Vector3(center.x, yUp, center.z);
                go.transform.localScale = new Vector3(sx, sy, sz);

                var col = go.GetComponent<Collider>(); if (col) Destroy(col);
                var rend = go.GetComponent<MeshRenderer>();
                if (rend)
                {
                    rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    rend.receiveShadows = false;
                    if (readyMaterial) rend.sharedMaterial = readyMaterial;
                }

                _tiles[cell] = new StartTile
                {
                    go = go,
                    rend = rend,
                    sy = sy,
                    yUp = yUp,
                    yDown = yDown
                };
            }
        }

        private void SetAllReady()
        {
            foreach (var kv in _tiles)
                SetPressed(kv.Value, pressed: false);
        }

        private void SetPressed(StartTile t, bool pressed)
        {
            if (!t.go) return;

            // Hauteur
            var p = t.go.transform.position;
            p.y = pressed ? t.yDown : t.yUp;
            t.go.transform.position = p;

            // Matériau
            if (t.rend)
            {
                if (pressed && activeMaterial) t.rend.sharedMaterial = activeMaterial;
                else if (!pressed && readyMaterial) t.rend.sharedMaterial = readyMaterial;
            }
        }

        // --- Reset global (reset de niveau) -------------------------------------

        public void ResetToInitial()
        {
            _isRunning = false;
            _sequenceSucceeded = false;
            _hasActive = false;
            _activeCell = default;

            SetAllReady();
        }


    }
}

