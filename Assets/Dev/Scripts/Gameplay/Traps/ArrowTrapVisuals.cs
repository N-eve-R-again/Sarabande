// FILE: Assets/DEV/Scripts/Traps/ArrowTrapVisuals.cs
using System.Collections.Generic;
using UnityEngine;
using Sarabande.Core;     // EdgeDirection
using Sarabande.Levels;

namespace Sarabande.Traps
{
    /// <summary>
    /// Visuels de pièges :
    /// - dalles (légèrement relevées, s'enfoncent à l'activation)
    /// - marqueurs horizontaux sur le HAUT des murs/bords pouvant tirer (centrés, sans indiquer la direction)
    /// À attacher sur LevelRoot. Purement visuel.
    /// </summary>
    public class ArrowTrapVisuals : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField, Min(0.001f)] private float cellSize = 1f;
        [SerializeField, Min(0.1f)] private float wallHeight = 1f;
        [SerializeField] private bool useLevelContext = true;
        [SerializeField] private Sarabande.Core.LevelContext levelContext;
        [SerializeField, HideInInspector] private Sarabande.Levels.LevelData levelData;

        [Header("Tile (dalle)")]
        [SerializeField] private Material tileMaterial;
        [SerializeField, Range(0.5f, 0.98f)] private float tileSizeScale = 0.88f; // < 1 pour plus petite que la case
        [SerializeField, Min(0.005f)] private float tileThickness = 0.02f;        // épaisseur visuelle
        [SerializeField, Min(0f)] private float tileUpLift = 0.01f;               // léger relief au repos
        [SerializeField, Min(0.01f)] private float tilePressDuration = 0.06f;
        [SerializeField, Min(0.01f)] private float tileReleaseDuration = 0.15f;

        [Header("Wall Top Marker (mur/bord émetteur)")]
        [SerializeField] private Material wallTopMaterial;
        [SerializeField, Range(0.2f, 0.95f)] private float topWidthScale = 0.7f;   // “longueur” du marqueur
        [SerializeField, Range(0.05f, 0.4f)] private float topDepthScale = 0.18f;  // “largeur” du marqueur
        [SerializeField, Min(0.005f)] private float topMarkerThickness = 0.01f;    // épaisseur (verticale)
        [SerializeField] private bool markBorders = true;                           // mettre un marqueur aussi sur les bords de map

        [SerializeField] private bool hideTrapPadOnDiscoCells = true;
        [SerializeField] private bool includeDiscoStartTiles = true;

        private Transform _tilesParent;
        private Transform _wallMarksParent;

        private HashSet<Vector2Int> _nonWalkableSet;              // murs pleins (cases non-walkable)
        private HashSet<Vector2Int> _placedWallMarks;             // pour éviter les doublons
        private HashSet<string> _placedBorderMarks;               // dédoublonnage des bords (clé: "x,y,dir")
        private HashSet<Vector2Int> _discoCells;

        private void Awake()
        {
            if (levelData == null) { Debug.LogError("[ArrowTrapVisuals] LevelData manquant."); enabled = false; return; }

            _tilesParent = new GameObject("TrapTiles").transform;
            _wallMarksParent = new GameObject("WallTopMarkers").transform;
            _tilesParent.SetParent(transform, false);
            _wallMarksParent.SetParent(transform, false);

            // set de murs (cases non-walkables)
            _nonWalkableSet = new HashSet<Vector2Int>();
            if (levelData.nonWalkables != null)
                foreach (var c in levelData.nonWalkables)
                    _nonWalkableSet.Add(new Vector2Int(c.x, c.z));

            _placedWallMarks = new HashSet<Vector2Int>();
            _placedBorderMarks = new HashSet<string>();

            BuildDiscoCellSet();
            BuildTiles();
            BuildWallTopMarkers();
        }

        private void BuildTiles()
        {
            if (levelData.arrowTraps == null) return;
            for (int i = 0; i < levelData.arrowTraps.Count; i++)
            {
                var spec = levelData.arrowTraps[i];
                var cell = new Vector2Int(spec.triggerCell.x, spec.triggerCell.z);
                Vector3 center = GridCenter(cell);

                if (hideTrapPadOnDiscoCells && IsDiscoCell(cell))
                {
                    // rien à construire pour cette dalle trap ; elle restera invisible,
                    // mais le piège se déclenchera toujours côté ArrowTrapSystem.
                    continue;
                }

                // géométrie : cube fin, < 1 case
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = $"TrapTile_{i}_({cell.x},{cell.y})";
                go.transform.SetParent(_tilesParent, false);

                float sx = tileSizeScale * cellSize;
                float sy = tileThickness;
                float sz = tileSizeScale * cellSize;

                // centres Y (repos/enfoncé)
                float yDownCenter = sy * 0.5f;                 // affleure le sol
                float yUpCenter = yDownCenter + tileUpLift;  // petit relief

                // place au repos
                go.transform.position = new Vector3(center.x, yUpCenter, center.z);
                go.transform.localScale = new Vector3(sx, sy, sz);

                // mat & ombres
                var col = go.GetComponent<Collider>(); if (col) Destroy(col);
                var mr = go.GetComponent<MeshRenderer>();
                if (mr)
                {
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    mr.receiveShadows = false;
                    if (tileMaterial) mr.sharedMaterial = tileMaterial;
                }

                // anim dalle
                var tv = go.AddComponent<TrapTileVisual>();
                tv.SetupCell(cell);
                tv.ConfigureHeights(yUpCenter, yDownCenter);
                tv.ConfigureDurations(tilePressDuration, tileReleaseDuration);
            }
        }

        private void BuildWallTopMarkers()
        {
            if (levelData.arrowTraps == null) return;

            float y = wallHeight + topMarkerThickness * 0.5f + 0.001f;

            for (int i = 0; i < levelData.arrowTraps.Count; i++)
            {
                var spec = levelData.arrowTraps[i];

                bool hasEmissions = (spec.emissions != null && spec.emissions.Count > 0);

                // --- si on a des emissions, on ignore le legacy ---
                if (!hasEmissions)
                {
                    var start = new Vector2Int(spec.startCell.x, spec.startCell.z);
                    var opposite = Opposite(spec.travelDir);
                    var off = OffsetFor(opposite);
                    var behind = start + off;

                    if (InsideBounds(behind) && _nonWalkableSet.Contains(behind))
                    {
                        if (_placedWallMarks.Add(behind))
                            CreateTopMarkerAtCell(behind, y);
                    }
                    else if (markBorders)
                    {
                        string key = $"{start.x},{start.y},{opposite}";
                        if (_placedBorderMarks.Add(key))
                            CreateTopMarkerOnBorder(start, opposite, y);
                    }
                }

                // --- NOUVEAU: markers pour chaque emission ---
                if (hasEmissions)
                {
                    foreach (var em in spec.emissions)
                    {
                        var emStart = new Vector2Int(em.startCell.x, em.startCell.z);
                        var emOpp = Opposite(em.travelDir);
                        var emOff = OffsetFor(emOpp);
                        var behind = emStart + emOff;

                        if (InsideBounds(behind) && _nonWalkableSet.Contains(behind))
                        {
                            if (_placedWallMarks.Add(behind))
                                CreateTopMarkerAtCell(behind, y);
                        }
                        else if (markBorders)
                        {
                            string key = $"{emStart.x},{emStart.y},{emOpp}";
                            if (_placedBorderMarks.Add(key))
                                CreateTopMarkerOnBorder(emStart, emOpp, y);
                        }
                    }
                }
            }
        }

        private void CreateTopMarkerAtCell(Vector2Int wallCell, float y)
        {
            Vector3 c = GridCenter(wallCell);
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = $"WallTop_{wallCell.x}_{wallCell.y}";
            go.transform.SetParent(_wallMarksParent, false);
            go.transform.position = new Vector3(c.x, y, c.z);
            go.transform.localScale = new Vector3(topWidthScale * cellSize, topMarkerThickness, topDepthScale * cellSize);

            var col = go.GetComponent<Collider>(); if (col) Destroy(col);
            var mr = go.GetComponent<MeshRenderer>();
            if (mr)
            {
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                if (wallTopMaterial) mr.sharedMaterial = wallTopMaterial;
            }
        }

        private void CreateTopMarkerOnBorder(Vector2Int startCell, EdgeDirection borderSide, float y)
        {
            Vector3 center = GridCenter(startCell);
            float half = cellSize * 0.5f;

            Vector3 pos = center;
            Vector3 scale;

            // On centre le marqueur sur l’arête, sans indiquer de direction.
            if (borderSide == EdgeDirection.North)
            {
                pos.z = center.z + half;
                scale = new Vector3(topWidthScale * cellSize, topMarkerThickness, topDepthScale * cellSize);
            }
            else if (borderSide == EdgeDirection.South)
            {
                pos.z = center.z - half;
                scale = new Vector3(topWidthScale * cellSize, topMarkerThickness, topDepthScale * cellSize);
            }
            else if (borderSide == EdgeDirection.East)
            {
                pos.x = center.x + half;
                scale = new Vector3(topDepthScale * cellSize, topMarkerThickness, topWidthScale * cellSize);
            }
            else // West
            {
                pos.x = center.x - half;
                scale = new Vector3(topDepthScale * cellSize, topMarkerThickness, topWidthScale * cellSize);
            }

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = $"BorderTop_{startCell.x}_{startCell.y}_{borderSide}";
            go.transform.SetParent(_wallMarksParent, false);
            go.transform.position = new Vector3(pos.x, y, pos.z);
            go.transform.localScale = scale;

            var col = go.GetComponent<Collider>(); if (col) Destroy(col);
            var mr = go.GetComponent<MeshRenderer>();
            if (mr)
            {
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                if (wallTopMaterial) mr.sharedMaterial = wallTopMaterial;
            }
        }

        private void BuildDiscoCellSet()
        {
            _discoCells = new HashSet<Vector2Int>();
            if (levelData == null) return;

            // 1) Toutes les cases des séquences disco
            if (levelData.discoSequences != null)
            {
                foreach (var seq in levelData.discoSequences)
                {
                    if (seq?.cells == null) continue;
                    foreach (var c in seq.cells)
                        _discoCells.Add(new Vector2Int(c.x, c.z));
                }
            }

            // 2) Optionnel : cases “start” disco
            if (includeDiscoStartTiles && levelData.discoStartTiles != null)
            {
                foreach (var c in levelData.discoStartTiles)
                    _discoCells.Add(new Vector2Int(c.x, c.z));
            }
        }

        private bool IsDiscoCell(Vector2Int cell)
        {
            return _discoCells != null && _discoCells.Contains(cell);
        }

        // --- Utils ---
        private Vector3 GridCenter(Vector2Int c)
            => new Vector3((c.x + 0.5f) * cellSize, 0f, (c.y + 0.5f) * cellSize);

        private bool InsideBounds(Vector2Int c)
            => c.x >= 0 && c.x < levelData.width && c.y >= 0 && c.y < levelData.height;

        private static Vector2Int OffsetFor(EdgeDirection dir) => dir switch
        {
            EdgeDirection.North => new Vector2Int(0, 1),
            EdgeDirection.South => new Vector2Int(0, -1),
            EdgeDirection.East => new Vector2Int(1, 0),
            EdgeDirection.West => new Vector2Int(-1, 0),
            _ => Vector2Int.zero
        };

        private static EdgeDirection Opposite(EdgeDirection d) => d switch
        {
            EdgeDirection.North => EdgeDirection.South,
            EdgeDirection.South => EdgeDirection.North,
            EdgeDirection.East => EdgeDirection.West,
            EdgeDirection.West => EdgeDirection.East,
            _ => d
        };

#if UNITY_EDITOR
        [ContextMenu("Rebuild Visuals")]
        private void RebuildVisuals()
        {
            ClearChildren(_tilesParent);
            ClearChildren(_wallMarksParent);
            _placedWallMarks?.Clear();
            _placedBorderMarks?.Clear();
            BuildTiles();
            BuildWallTopMarkers();
        }

        private static void ClearChildren(Transform t)
        {
            if (t == null) return;
            if (!Application.isPlaying)
            {
                for (int i = t.childCount - 1; i >= 0; i--)
                    DestroyImmediate(t.GetChild(i).gameObject);
            }
            else
            {
                for (int i = t.childCount - 1; i >= 0; i--)
                    Destroy(t.GetChild(i).gameObject);
            }
        }
#endif
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
            if (Application.isPlaying)
            {
                BuildDiscoCellSet();  // <-- NOUVEAU
            }
            // NOTE: si ce système a besoin de se "rebuild" quand le LevelData change,
            // appelle ici ta méthode interne (ex: RebuildFromLevelData()).
        }
        private void OnEnable() { AttachContext(); }
        private void OnDisable() { DetachContext(); }
#if UNITY_EDITOR
        private void OnValidate() { if (!Application.isPlaying) AttachContext(); }
#endif
    }
}
