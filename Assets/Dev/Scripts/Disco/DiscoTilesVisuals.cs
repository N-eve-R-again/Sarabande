// FILE: Assets/DEV/Scripts/Disco/DiscoTilesVisuals.cs
//
// Rôle (résumé)
// - Construit et pilote les dalles “disco” visibles d’après LevelData.discoSequences.
// - Pour chaque cellule, instancie un DiscoTile (root + “Base” + “Core”) via DiscoTileVisual.
// - Expose des méthodes simples pour : tout éteindre, passer une tuile à Off/On/Next,
//   remplir visuellement le “Core” de la tuile NEXT (progression), et remettre tous les cores à zéro.
// - Se rattache au LevelContext (facultatif) pour recevoir le LevelData.
//
// Invariants
// - Aucun renommage de champs sérialisés ni de méthodes publiques.
// - Logique strictement identique ; uniquement commentaires, ordre plus lisible, et renommages locaux.

using System.Collections.Generic;
using UnityEngine;
using Sarabande.Levels;

namespace Sarabande.Disco
{
    public class DiscoTilesVisuals : MonoBehaviour
    {
        // ?????????????????????????????????????????????????????????????????????????????
        // Data & Context (sérialisé)
        // ?????????????????????????????????????????????????????????????????????????????

        [Header("Data")]
        [SerializeField, Min(0.001f)] private float cellSize = 1f;
        [SerializeField] private bool useLevelContext = true;
        [SerializeField] private Sarabande.Core.LevelContext levelContext;
        [SerializeField, HideInInspector] private Sarabande.Levels.LevelData levelData;

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

        [Header("Palette (Materials)")]
        [SerializeField] private Material[] colorMaterials;

        [Header("Next Core Sizing")]
        [Tooltip("1.0 = le Core plein occupe toute la dalle ; 0.95 = laisse un liseré (évite aliasing).")]
        [SerializeField, Range(0.5f, 1.2f)] private float coreFullRelativeToTile = 0.96f;

        // ?????????????????????????????????????????????????????????????????????????????
        // Runtime (non sérialisé)
        // ?????????????????????????????????????????????????????????????????????????????

        private Transform _parent; // conteneur "DiscoTiles"
        private readonly Dictionary<Vector2Int, DiscoTileVisual> _tiles = new();     // cell -> component
        private readonly Dictionary<Vector2Int, Transform> _coreByCell = new();      // cache: cell -> Core transform

        // ?????????????????????????????????????????????????????????????????????????????
        // Unity lifecycle
        // ?????????????????????????????????????????????????????????????????????????????

        private void Awake()
        {
            if (!levelData)
            {
                Debug.LogError("[DiscoTilesVisuals] LevelData manquant.");
                enabled = false;
                return;
            }
            EnsureParent();
            BuildAll();
        }

        private void OnEnable() { AttachContext(); }
        private void OnDisable() { DetachContext(); }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying) AttachContext();
        }
#endif

        // ?????????????????????????????????????????????????????????????????????????????
        // Construction
        // ?????????????????????????????????????????????????????????????????????????????

        /// <summary>Assure l’existence du conteneur <c>DiscoTiles</c> (créé si manquant).</summary>
        private void EnsureParent()
        {
            if (_parent != null) return;

            var existing = transform.Find("DiscoTiles");
            if (existing != null)
            {
                _parent = existing;
            }
            else
            {
                var parentGo = new GameObject("DiscoTiles");
                _parent = parentGo.transform;
                _parent.SetParent(transform, false);
            }
        }

        /// <summary>Supprime tous les enfants du conteneur (Destroy/DestroyImmediate selon le contexte).</summary>
        private void ClearChildren()
        {
            if (_parent == null) return;

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

        /// <summary>Retourne un matériau aléatoire depuis <see cref="colorMaterials"/> (ou null si vide).</summary>
        private Material RandMat()
        {
            if (colorMaterials == null || colorMaterials.Length == 0) return null;
            return colorMaterials[Random.Range(0, colorMaterials.Length)];
        }

        /// <summary>
        /// Construit les dalles pour toutes les cellules présentes dans <c>LevelData.discoSequences</c>
        /// (uniques par cellule).
        /// </summary>
        private void BuildAll()
        {
            EnsureParent();
            ClearChildren();
            _tiles.Clear();

            var uniques = new HashSet<Vector2Int>();
            int created = 0;

            if (levelData.discoSequences != null)
            {
                foreach (var sequence in levelData.discoSequences)
                {
                    if (sequence == null || sequence.cells == null) continue;

                    foreach (var gridCoord in sequence.cells)
                    {
                        var cell = new Vector2Int(gridCoord.x, gridCoord.z); // X/Z du LevelData
                        if (!uniques.Add(cell)) continue;

                        var tile = BuildOne(cell);
                        _tiles[cell] = tile;
                        created++;
                    }
                }
            }

            if (created == 0)
                Debug.LogWarning("[DiscoTilesVisuals] Aucune dalle disco construite. Vérifie LevelData.discoSequences[*].cells.");
        }

        /// <summary>Instancie un DiscoTileVisual à la position monde de <paramref name="cell"/>.</summary>
        private DiscoTileVisual BuildOne(Vector2Int cell)
        {
            var rootGo = new GameObject($"DiscoTile_{cell.x}_{cell.y}");
            rootGo.transform.SetParent(_parent, false);
            rootGo.transform.position = GridCenter(cell);

            var tileVisual = rootGo.AddComponent<DiscoTileVisual>();
            tileVisual.Setup(
                cell, cellSize,
                tileSizeScale, coreSizeScale, tileThickness, tileLift,
                baseMaterial, coreMaterial, baseOffColor,
                nextIntensity, onIntensity
            );
            return tileVisual;
        }

        /// <summary>Centre monde d’une cellule (basé sur <see cref="cellSize"/>).</summary>
        private Vector3 GridCenter(Vector2Int c)
            => new Vector3((c.x + 0.5f) * cellSize, 0f, (c.y + 0.5f) * cellSize);

        /// <summary>Couleur vive pseudo-aléatoire dans la palette (fallback: blanc).</summary>
        private Color RandColor()
        {
            if (palette == null || palette.Length == 0) return Color.white;
            return palette[Random.Range(0, palette.Length)];
        }

        // ?????????????????????????????????????????????????????????????????????????????
        // Preview helpers (Editor)
        // ?????????????????????????????????????????????????????????????????????????????

        [ContextMenu("Disco: Rebuild & Preview (1=ON, 2=NEXT)")]
        private void PreviewRebuild()
        {
            EnsureParent();
            BuildAll();

            Debug.Log($"[DiscoTilesVisuals] Dalles construites: {_tiles.Count}");
            foreach (var entry in _tiles) entry.Value.SetOff();

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
            foreach (var entry in _tiles) entry.Value.SetOff();
        }

        // ?????????????????????????????????????????????????????????????????????????????
        // API publique (utilisée par DiscoSequenceSystem)
        // ?????????????????????????????????????????????????????????????????????????????

        /// <summary>Récupère le <see cref="DiscoTileVisual"/> d’une cellule, ou <c>null</c> si non construit.</summary>
        public DiscoTileVisual GetTile(Vector2Int c)
            => _tiles.TryGetValue(c, out var tile) ? tile : null;

        /// <summary>Met toutes les dalles à l’état OFF (utile au reset ou au (re)start de séquence).</summary>
        public void SetAllOff()
        {
            foreach (var entry in _tiles)
                entry.Value.SetOff();
        }

        /// <summary>
        /// Change l’état visuel d’UNE cellule.
        /// Si une palette de matériaux est fournie, elle est prioritaire (émissive) ;
        /// sinon on applique la couleur passée en paramètre (fallback).
        /// </summary>
        /// <param name="cell">Cellule cible.</param>
        /// <param name="s">État à appliquer (Off/On/Next).</param>
        /// <param name="color">Couleur fallback si aucun matériau palette n’est fourni.</param>
        public void SetState(Vector2Int cell, State s, Color color)
        {
            if (!_tiles.TryGetValue(cell, out var tileParts)) return;

            Material paletteMat = RandMat();

            switch (s)
            {
                case State.Off:
                    tileParts.SetOff();
                    break;

                case State.On:
                    if (paletteMat) tileParts.SetOn(paletteMat);
                    else tileParts.SetOn(color);
                    break;

                case State.Next:
                    if (paletteMat) tileParts.SetNext(paletteMat);
                    else tileParts.SetNext(color);
                    break;
            }
        }

        /// <summary>
        /// Ajuste le remplissage (taille) du “Core” de la tuile NEXT.
        /// <para>
        /// <paramref name="fraction"/> est la fraction de la dalle (0..1). La taille réelle
        /// est modulée par <see cref="coreFullRelativeToTile"/> pour garder un liseré.
        /// </para>
        /// </summary>
        /// <param name="cell">Cellule dont on ajuste le Core.</param>
        /// <param name="fraction">Fraction 0..1 de la dalle à occuper.</param>
        public void SetNextCoreFill(Vector2Int cell, float fraction)
        {
            var baseTf = TryGetBaseTransform(cell);
            var coreTf = TryGetCoreTransform(cell);
            if (!baseTf || !coreTf) return;

            fraction = Mathf.Clamp01(fraction);

            // Taille cible = taille de la “Base” * facteur de marge * fraction
            Vector3 baseLS = baseTf.localScale;
            float targetX = baseLS.x * coreFullRelativeToTile * fraction;
            float targetZ = baseLS.z * coreFullRelativeToTile * fraction;

            Vector3 coreLS = coreTf.localScale;
            coreTf.localScale = new Vector3(targetX, coreLS.y, targetZ);
        }

        /// <summary>Remet à zéro la taille de tous les cores (même ceux pas encore en cache).</summary>
        public void ClearAllNextCores()
        {
            foreach (var entry in _tiles)
            {
                var cell = entry.Key;
                var coreTf = TryGetCoreTransform(cell);
                if (!coreTf) continue;

                Vector3 coreLS = coreTf.localScale;
                coreTf.localScale = new Vector3(0f, coreLS.y, 0f);
            }
            // On conserve le cache _coreByCell (évite de futurs Find()).
        }

        // ?????????????????????????????????????????????????????????????????????????????
        // Helpers internes
        // ?????????????????????????????????????????????????????????????????????????????

        /// <summary>Retourne (et met en cache) le Transform du “Core” d’une tuile.</summary>
        private Transform TryGetCoreTransform(Vector2Int cell)
        {
            if (_coreByCell.TryGetValue(cell, out var cached) && cached) return cached;

            var tile = GetTile(cell);
            if (!tile) return null;

            // IMPORTANT: on cherche sur le transform du tile (enfants nommés)
            var coreTf = tile.transform.Find("Core");
            if (coreTf) _coreByCell[cell] = coreTf;
            return coreTf;
        }

        /// <summary>Retourne le Transform de la “Base” d’une tuile.</summary>
        private Transform TryGetBaseTransform(Vector2Int cell)
        {
            var tile = GetTile(cell);
            if (!tile) return null;
            return tile.transform.Find("Base");
        }

        // ?????????????????????????????????????????????????????????????????????????????
        // LevelContext plumbing
        // ?????????????????????????????????????????????????????????????????????????????

        private void AttachContext()
        {
            if (!useLevelContext) return;

            if (!levelContext)
                levelContext = GetComponentInParent<Sarabande.Core.LevelContext>();

            if (levelContext != null)
            {
                levelContext.LevelDataChanged += HandleContextLevelDataChanged;
                HandleContextLevelDataChanged(levelContext.LevelData); // init immédiate
            }
            else
            {
                Debug.LogWarning($"[{GetType().Name}] Aucun LevelContext parent trouvé.");
            }
        }

        private void DetachContext()
        {
            if (levelContext != null)
                levelContext.LevelDataChanged -= HandleContextLevelDataChanged;
        }

        private void HandleContextLevelDataChanged(Sarabande.Levels.LevelData ld)
        {
            if (levelData == ld) return;
            levelData = ld;

#if UNITY_EDITOR
            if (!Application.isPlaying)
                UnityEditor.EditorUtility.SetDirty(this); // l’inspector reflète la maj auto
#endif
            // Note: reconstruire ici si tu veux supporter le hot-reload des dalles.
            // Ex: BuildAll();
        }

        // ?????????????????????????????????????????????????????????????????????????????
        // Types publics
        // ?????????????????????????????????????????????????????????????????????????????

        public enum State { Off, On, Next }
    }
}
