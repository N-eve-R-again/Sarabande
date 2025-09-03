// FILE: Assets/DEV/Scripts/Disco/DiscoTilesVisuals.cs
using System.Collections.Generic;
using UnityEngine;
using Sarabande.Levels;

namespace Sarabande.Disco
{
    public class DiscoTilesVisuals : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private LevelData levelData;
        [SerializeField, Min(0.001f)] private float cellSize = 1f;

        [Header("Materials (optionnels)")]
        [SerializeField] private Material baseMaterial;
        [SerializeField] private Material coreMaterial;

        [Header("Geometry (defaults tuned for visibility)")]
        [SerializeField, Range(0.4f, 0.98f)] private float tileSizeScale = 0.90f;
        [SerializeField, Range(0.2f, 0.95f)] private float coreSizeScale = 0.55f;
        [SerializeField, Min(0.002f)] private float tileThickness = 0.03f;
        [SerializeField, Min(0f)] private float tileLift = 0.02f;

        [Header("Colors")]
        [SerializeField] private Color baseOffColor = new Color(0.20f, 0.20f, 0.22f, 1f);
        [SerializeField, Min(0.05f)] private float nextIntensity = 0.7f;
        [SerializeField, Min(0.1f)] private float onIntensity = 1.8f;

        [Tooltip("Palette disco (choisie aléatoirement pour l'état ON/NEXT).")]
        [SerializeField]
        private Color[] palette = new Color[]
        {
            new Color(1.00f, 0.20f, 0.35f),
            new Color(1.00f, 0.55f, 0.10f),
            new Color(0.95f, 0.95f, 0.20f),
            new Color(0.10f, 0.85f, 0.25f),
            new Color(0.10f, 0.65f, 1.00f),
            new Color(0.45f, 0.30f, 1.00f),
            new Color(1.00f, 0.20f, 0.80f)
        };

        private Transform _parent; // "DiscoTiles"
        private readonly Dictionary<Vector2Int, DiscoTileVisual> _tiles = new();

        public DiscoTileVisual GetTile(Vector2Int c)
            => _tiles.TryGetValue(c, out var t) ? t : null;

        public enum State { Off, On, Next }

        private void Awake()
        {
            if (!levelData)
            {
                Debug.LogError("[DiscoTilesVisuals] LevelData manquant.");
                enabled = false; return;
            }
            EnsureParent();
            BuildAll();
        }

        private void EnsureParent()
        {
            // Recrée le parent au besoin (même en Edit Mode / après recompilation)
            if (_parent != null) return;
            var existing = transform.Find("DiscoTiles");
            if (existing != null) _parent = existing;
            else
            {
                var go = new GameObject("DiscoTiles");
                _parent = go.transform;
                _parent.SetParent(transform, false);
            }
        }

        private void ClearChildren()
        {
            if (_parent == null) return;
            // En Editor: DestroyImmediate, en Play: Destroy
            if (Application.isPlaying)
            {
                for (int i = _parent.childCount - 1; i >= 0; i--)
                    Destroy(_parent.GetChild(i).gameObject);
            }
            else
            {
                for (int i = _parent.childCount - 1; i >= 0; i--)
                    DestroyImmediate(_parent.GetChild(i).gameObject);
            }
        }

        private void BuildAll()
        {
            EnsureParent();
            ClearChildren();
            _tiles.Clear();

            var uniques = new HashSet<Vector2Int>();
            int created = 0;

            if (levelData.discoSequences != null)
            {
                foreach (var seq in levelData.discoSequences)
                {
                    if (seq == null || seq.cells == null) continue;
                    foreach (var gc in seq.cells)
                    {
                        var cell = new Vector2Int(gc.x, gc.z); // on lit X/Z
                        if (!uniques.Add(cell)) continue;
                        var t = BuildOne(cell);
                        _tiles[cell] = t;
                        created++;
                    }
                }
            }

            if (created == 0)
                Debug.LogWarning("[DiscoTilesVisuals] Aucune dalle disco construite. Vérifie LevelData.discoSequences[*].cells.");
        }

        private DiscoTileVisual BuildOne(Vector2Int cell)
        {
            var root = new GameObject($"DiscoTile_{cell.x}_{cell.y}");
            root.transform.SetParent(_parent, false);
            root.transform.position = GridCenter(cell);

            var tv = root.AddComponent<DiscoTileVisual>();
            tv.Setup(cell, cellSize,
                     tileSizeScale, coreSizeScale, tileThickness, tileLift,
                     baseMaterial, coreMaterial, baseOffColor,
                     nextIntensity, onIntensity);
            return tv;
        }

        private Vector3 GridCenter(Vector2Int c)
            => new Vector3((c.x + 0.5f) * cellSize, 0f, (c.y + 0.5f) * cellSize);

        private Color RandColor()
        {
            if (palette == null || palette.Length == 0) return Color.white;
            return palette[Random.Range(0, palette.Length)];
        }

        // --- Prévisualisation pratique ---
        [ContextMenu("Disco: Rebuild & Preview (1=ON, 2=NEXT)")]
        private void PreviewRebuild()
        {
            EnsureParent();
            BuildAll();

            Debug.Log($"[DiscoTilesVisuals] Dalles construites: {_tiles.Count}");
            foreach (var kv in _tiles) kv.Value.SetOff();

            if (_tiles.Count == 0) return;

            var ordered = new List<Vector2Int>(_tiles.Keys);
            ordered.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));

            if (ordered.Count >= 1) _tiles[ordered[0]].SetOn(RandColor());
            if (ordered.Count >= 2) _tiles[ordered[1]].SetNext(RandColor());
        }

        [ContextMenu("Disco: All OFF")]
        private void PreviewAllOff()
        {
            EnsureParent();
            foreach (var kv in _tiles) kv.Value.SetOff();
        }
        // Met tout OFF (utile au reset / (re)start)
        public void SetAllOff()
        {
            foreach (var kv in _tiles) // <-- suppose que tu as déjà un dict cell->parts
                kv.Value.SetOff();
        }

        // Change l’état d’UNE case (couleur optionnelle pour ON/NEXT)
        public void SetState(Vector2Int cell, State s, Color color)
        {
            if (!_tiles.TryGetValue(cell, out var parts)) return;

            switch (s)
            {
                case State.Off:
                    parts.SetOff();
                    break;
                case State.On:
                    parts.SetOn(color);
                    break;
                case State.Next:
                    parts.SetNext(color);
                    break;
            }
        }
    }
}
