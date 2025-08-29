using System.Collections.Generic;
using UnityEngine;
using Sarabande.Core;

namespace Sarabande.Levels
{
    /// <summary>
    /// Construit la grille visible, les murs (non-walkables) et les murs fins à partir d'un LevelData.
    /// À attacher sur LevelRoot dans la scène.
    /// </summary>
    public class LevelLoader : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private LevelData levelData;

        [Header("Grid Visuals")]
        [SerializeField, Min(0.001f)] private float cellSize = 1f;
        [SerializeField, Min(0.001f)] private float lineWidth = 0.03f;
        [SerializeField] private float lineY = 0.01f; // décoller un peu du sol pour éviter le z-fighting
        [SerializeField] private Material lineMaterial;

        [Header("Walls")]
        [SerializeField, Min(0f)] private float wallInset = 0.05f;
        [SerializeField, Min(0.1f)] private float wallHeight = 1f;
        [SerializeField, Min(0.01f)] private float thinThickness = 0.10f;

        [Header("Wall Materials")]
        [SerializeField] private Material wallMaterial;
        [SerializeField] private Material thinWallMaterial;

        [Header("Debug Markers")]
        [SerializeField] private bool showMarkers = true;
        [SerializeField] private Material heroMarkerMaterial;
        [SerializeField] private Material nmeMarkerMaterial;
        [SerializeField] private Material exitMarkerMaterial;
        [SerializeField, Min(0f)] private float markerY = 0.02f;         // hauteur au-dessus du sol
        [SerializeField, Min(0.05f)] private float markerDiameter = 0.6f; // diamètre des disques
        [SerializeField, Min(0.05f)] private float exitMarkerSize = 0.25f; // taille du cube "exit"

        [Header("Layers")]
        [SerializeField] private string obstaclesLayerName = "Obstacles";

        // Parents pour garder la hiérarchie propre
        private Transform _gridParent;
        private Transform _wallsParent;
        private Transform _thinWallsParent;

        private void Awake()
        {
            if (levelData == null)
            {
                Debug.LogError("[LevelLoader] LevelData manquant.");
                return;
            }

            // Nettoie d'anciens builds si on relance en Play plusieurs fois
            ClearChildren();

            // Crée des dossiers vides dans la hiérarchie
            _gridParent = new GameObject("GridLines").transform;
            _wallsParent = new GameObject("Walls").transform;
            _thinWallsParent = new GameObject("ThinWalls").transform;
            _gridParent.SetParent(transform, false);
            _wallsParent.SetParent(transform, false);
            _thinWallsParent.SetParent(transform, false);

            BuildGridLines();
            BuildWalls();
            BuildThinWalls();

            if (showMarkers) BuildDebugMarkers();

            Debug.Log("[LevelLoader] Build terminé.");
        }

        private void ClearChildren()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i).gameObject;

                if (Application.isPlaying)
                {
                    // En jeu : destruction sûre en fin de frame
                    Destroy(child);
                }
                else
                {
                    // À l'arrêt (éditeur) : on peut nettoyer instantanément
#if UNITY_EDITOR
                    DestroyImmediate(child);
#endif
                }
            }
        }

        private void BuildGridLines()
        {
            int w = levelData.width;
            int h = levelData.height;

            // Lignes verticales (x constant, z de 0 à h)
            for (int x = 0; x <= w; x++)
            {
                var go = new GameObject($"VLine_{x}");
                go.transform.SetParent(_gridParent, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.positionCount = 2;
                lr.useWorldSpace = true;
                lr.material = lineMaterial;
                lr.startWidth = lr.endWidth = lineWidth;
                lr.numCapVertices = 2; // bouts arrondis
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;

                lr.SetPosition(0, new Vector3(x * cellSize, lineY, 0f));
                lr.SetPosition(1, new Vector3(x * cellSize, lineY, h * cellSize));
            }

            // Lignes horizontales (z constant, x de 0 à w)
            for (int z = 0; z <= h; z++)
            {
                var go = new GameObject($"HLine_{z}");
                go.transform.SetParent(_gridParent, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.positionCount = 2;
                lr.useWorldSpace = true;
                lr.material = lineMaterial;
                lr.startWidth = lr.endWidth = lineWidth;
                lr.numCapVertices = 2;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;

                lr.SetPosition(0, new Vector3(0f, lineY, z * cellSize));
                lr.SetPosition(1, new Vector3(w * cellSize, lineY, z * cellSize));
            }
        }

        private void BuildWalls()
        {
            // Dé-duplication pour éviter les doublons saisis par erreur
            var set = new HashSet<(int x, int z)>();
            foreach (var c in levelData.nonWalkables)
            {
                if (!set.Add((c.x, c.z)))
                {
                    Debug.LogWarning($"[LevelLoader] Doublon nonWalkable ignoré en ({c.x},{c.z}).");
                    continue;
                }

                // On instancie un cube 1x1xwallHeight, centré sur la case
                var pos = GridCenter(c);
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = $"Wall_{c.x}_{c.z}";
                go.transform.SetParent(_wallsParent, false);
                go.transform.position = new Vector3(pos.x, wallHeight * 0.5f, pos.z);
                float sx = Mathf.Max(0.001f, cellSize - 2f * wallInset);
                float sz = Mathf.Max(0.001f, cellSize - 2f * wallInset);
                go.transform.localScale = new Vector3(sx, wallHeight, sz);

                //assignation de layer
                int obsLayer = LayerMask.NameToLayer(obstaclesLayerName);
                if (obsLayer != -1) go.layer = obsLayer;
                else Debug.LogWarning($"[LevelLoader] Layer '{obstaclesLayerName}' introuvable. Crée-le dans Project Settings > Tags and Layers.");

                // Optionnel : on peut désactiver l'ombre pour un look "editeur"
                var mr = go.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    mr.receiveShadows = false;
                    if (wallMaterial != null) mr.sharedMaterial = wallMaterial;
                }
            }
        }

        private void BuildThinWalls()
        {
            // Dé-duplication (un même segment ajouté deux fois)
            var seen = new HashSet<(int ax, int az, int bx, int bz)>();

            foreach (var e in levelData.thinWalls)
            {
                // Normaliser l'ordre pour la HashSet
                var key = (ax: Mathf.Min(e.a.x, e.b.x), az: Mathf.Min(e.a.z, e.b.z),
                           bx: Mathf.Max(e.a.x, e.b.x), bz: Mathf.Max(e.a.z, e.b.z));
                if (!seen.Add(key))
                {
                    Debug.LogWarning($"[LevelLoader] Doublon thinWall ignoré entre ({e.a.x},{e.a.z}) et ({e.b.x},{e.b.z}).");
                    continue;
                }

                // Vérification adjacency (même x ou même z, distance 1)
                bool sameX = e.a.x == e.b.x && Mathf.Abs(e.a.z - e.b.z) == 1;
                bool sameZ = e.a.z == e.b.z && Mathf.Abs(e.a.x - e.b.x) == 1;
                if (!sameX && !sameZ)
                {
                    Debug.LogError($"[LevelLoader] thinWall non-adjacent entre ({e.a.x},{e.a.z}) et ({e.b.x},{e.b.z})");
                    continue;
                }

                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = $"Thin_{e.a.x}_{e.a.z}__{e.b.x}_{e.b.z}";
                go.transform.SetParent(_thinWallsParent, false);

                //assignation de layer
                int obsLayer = LayerMask.NameToLayer(obstaclesLayerName);
                if (obsLayer != -1) go.layer = obsLayer;
                else Debug.LogWarning($"[LevelLoader] Layer '{obstaclesLayerName}' introuvable. Crée-le dans Project Settings > Tags and Layers.");

                // Position & scale : une "barre" mince posée sur l'arête
                if (sameX)
                {
                    // Séparation horizontale entre deux rangées : x au centre de la colonne, z sur la ligne entre les 2 cases
                    float x = e.a.x * cellSize + cellSize * 0.5f;
                    float z = Mathf.Min(e.a.z, e.b.z) * cellSize + cellSize; // ligne entre z et z+1
                    go.transform.position = new Vector3(x, wallHeight * 0.5f, z);
                    go.transform.localScale = new Vector3(cellSize, wallHeight, thinThickness);
                }
                else // sameZ
                {
                    // Séparation verticale entre deux colonnes : z au centre de la rangée, x sur la ligne entre les 2 cases
                    float x = Mathf.Min(e.a.x, e.b.x) * cellSize + cellSize; // ligne entre x et x+1
                    float z = e.a.z * cellSize + cellSize * 0.5f;
                    go.transform.position = new Vector3(x, wallHeight * 0.5f, z);
                    go.transform.localScale = new Vector3(thinThickness, wallHeight, cellSize);
                }

                var mr = go.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    mr.receiveShadows = false;
                    if (thinWallMaterial != null) mr.sharedMaterial = thinWallMaterial;
                    else if (wallMaterial != null) mr.sharedMaterial = wallMaterial;
                }
            }
        }

        /// <summary>Centre monde de la case (x,z).</summary>
        private Vector3 GridCenter(GridCoord c)
        {
            return new Vector3((c.x + 0.5f) * cellSize, 0f, (c.z + 0.5f) * cellSize);
        }

        private void BuildDebugMarkers()
        {
            var parent = new GameObject("Markers").transform;
            parent.SetParent(transform, false);

            // HÉRO
            CreateDiscMarker("HeroSpawn", levelData.heroSpawn, heroMarkerMaterial, parent);

            // NME
            CreateDiscMarker("NMESpawn", levelData.nmeSpawn, nmeMarkerMaterial, parent);

            // Sortie
            CreateExitMarker(parent);
        }

        private void CreateDiscMarker(string name, GridCoord c, Material mat, Transform parent)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);

            // centre de case
            var center = GridCenter(c);

            // Le mesh Cylinder fait 2 unités de haut par défaut ? scale.y = 0.01f => hauteur ~0.02
            float halfHeight = 0.01f;
            go.transform.position = new Vector3(center.x, markerY + halfHeight, center.z);
            go.transform.localScale = new Vector3(markerDiameter, 0.01f, markerDiameter);

            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                if (mat != null) mr.sharedMaterial = mat;
            }
        }

        private void CreateExitMarker(Transform parent)
        {
            var cell = levelData.exit.fromCell;
            var dir = levelData.exit.direction;

            var center = GridCenter(cell);
            var pos = center;

            // Place le marqueur sur le bord extérieur de la case dans la direction d'Exit
            float half = cellSize * 0.5f;
            switch (dir)
            {
                case EdgeDirection.East: pos.x += half; break;
                case EdgeDirection.West: pos.x -= half; break;
                case EdgeDirection.North: pos.z += half; break;
                case EdgeDirection.South: pos.z -= half; break;
            }

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "ExitMarker";
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(pos.x, markerY + 0.05f, pos.z);
            go.transform.localScale = new Vector3(exitMarkerSize, 0.1f, exitMarkerSize);

            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                if (exitMarkerMaterial != null) mr.sharedMaterial = exitMarkerMaterial;
            }
        }

    }
}
