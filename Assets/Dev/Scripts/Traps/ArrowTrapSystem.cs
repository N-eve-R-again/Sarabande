using System.Collections.Generic;
using UnityEngine;
using Sarabande.Levels;
using Sarabande.Player;
using Sarabande.NME;
using Sarabande.Core;   // <-- pour EdgeDirection
using Sarabande.Traps;
using static Sarabande.Core.GridUtils;

namespace Sarabande.Traps
{
    /// <summary>
    /// Observe HÉRO + NME ; déclenche les flèches quand on entre sur les cases trigger.
    /// - Une flèche par entrée sur une dalle (déclenchement “edge”).
    /// - Respecte canRearm/rearmDelay par piège.
    /// - Spawne un ArrowProjectile paramétré (vitesse, dir, masque obstacles).
    /// À attacher sur LevelRoot.
    /// </summary>
    public class ArrowTrapSystem : MonoBehaviour, Sarabande.Core.IResettable
    {
        [Header("Data & Refs")]
        [SerializeField] private LevelData levelData;
        [SerializeField, Min(0.001f)] private float cellSize = 1f;
        [SerializeField] private HeroController hero;
        [SerializeField] private Sarabande.Core.ResetManager resetManager;

        [Header("Visuals")]
        [SerializeField] private Material arrowMaterial;          // placeholder
        [SerializeField, Min(0.02f)] private float arrowThickness = 0.08f;
        [SerializeField, Min(0.1f)] private float arrowLengthInCell = 0.7f; // longueur visuelle

        [Header("Sprite (projectile)")]
        [SerializeField] private Sprite arrowSprite;                 // sprite "North"
        [SerializeField, Range(0.1f, 2f)] private float spriteScale = 0.8f;
        [SerializeField] private float spriteYOffset = 0.02f;
        [SerializeField] private string spriteSortingLayer = "Default";
        [SerializeField] private int spriteOrderInLayer = 50;

        [Header("Collision")]
        [SerializeField] private LayerMask obstaclesMask;         // coche "Obstacles"

        [Header("Layer (optionnel)")]
        [SerializeField] private string projectilesLayerName = "Projectiles";

        private NMEController[] _nmes;
        private System.Collections.Generic.Dictionary<Vector2Int, TrapTileVisual> _tileVisuals;

        // état runtime des pièges
        private struct TrapRuntime
        {
            public bool armed;
            public float nextReadyTime;
        }
        private TrapRuntime[] _runtime;

        // mémoires d’entrée de case (edge detection)
        private Vector2Int _lastHeroCell;
        private readonly Dictionary<NMEController, Vector2Int> _lastNmeCell = new();

        private void Start()
        {
            if (levelData == null || hero == null)
            {
                Debug.LogError("[ArrowTrapSystem] LevelData ou Hero manquant.");
                enabled = false;
                return;
            }

            _nmes = FindObjectsByType<NMEController>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            int count = levelData.arrowTraps?.Count ?? 0;
            _runtime = new TrapRuntime[count];
            _tileVisuals = new System.Collections.Generic.Dictionary<Vector2Int, TrapTileVisual>();
            var tiles = FindObjectsByType<TrapTileVisual>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var tv in tiles)
                if (tv != null)
                    _tileVisuals[tv.Cell] = tv;

            for (int i = 0; i < count; i++)
            {
                _runtime[i].armed = true;
                _runtime[i].nextReadyTime = 0f;
            }

            _lastHeroCell = hero.GridPos;
            if (_nmes != null)
            {
                foreach (var n in _nmes)
                    if (n != null)
                        _lastNmeCell[n] = n.GridPos;
            }
        }

        private void Update()
        {
            // réarmement éventuel
            for (int i = 0; i < _runtime.Length; i++)
            {
                if (!_runtime[i].armed && Time.time >= _runtime[i].nextReadyTime)
                {
                    var spec = levelData.arrowTraps[i];
                    if (spec.canRearm) _runtime[i].armed = true;
                }
            }

            // Héro : détection d’entrée de case
            Vector2Int heroCell = hero.GridPos;
            if (heroCell != _lastHeroCell)
            {
                TryTriggerAtCell(heroCell);
                _lastHeroCell = heroCell;
            }

            // NME : détection d’entrée de case
            if (_nmes != null)
            {
                foreach (var n in _nmes)
                {
                    if (n == null) continue;
                    Vector2Int cell = n.GridPos;
                    if (_lastNmeCell.TryGetValue(n, out var prev))
                    {
                        if (cell != prev)
                        {
                            TryTriggerAtCell(cell);
                            _lastNmeCell[n] = cell;
                        }
                    }
                    else
                    {
                        _lastNmeCell[n] = cell;
                    }
                }
            }
        }

        private void TryTriggerAtCell(Vector2Int enteredCell)
        {
            for (int i = 0; i < _runtime.Length; i++)
            {
                if (!_runtime[i].armed) continue;
                var spec = levelData.arrowTraps[i];
                if (enteredCell.x == spec.triggerCell.x && enteredCell.y == spec.triggerCell.z)
                {
                    FireTrap(i, spec);
                }
            }
        }

        private void FireTrap(int index, LevelData.ArrowTrapSpec spec)
        {
            // désarme + programme réarmement si nécessaire
            _runtime[index].armed = false;
            if (spec.canRearm)
                _runtime[index].nextReadyTime = Time.time + spec.rearmDelay;

            // ... (juste après avoir armé/désarmé)
            var triggerCell = new Vector2Int(spec.triggerCell.x, spec.triggerCell.z);
            if (_tileVisuals != null && _tileVisuals.TryGetValue(triggerCell, out var tile))
            {
                tile.PressAndHide();                // AVANT: tile.Press();
                if (spec.canRearm)
                    StartCoroutine(RearmTile(tile, spec.rearmDelay));
            }

            Sarabande.Core.NoiseSystem.Emit(triggerCell);

            // --- spawn flèche (sprite à plat) ---
            Vector3 startPos = Center(new Vector2Int(spec.startCell.x, spec.startCell.z), cellSize) + Vector3.up * 0.02f;
            Vector3 dir = DirToWorld(spec.travelDir);

            // Parent qui porte la rotation (yaw) = direction de déplacement
            var go = new GameObject($"Arrow_{index}");
            go.transform.position = startPos;
            go.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);

            // Layer projectiles (parent + enfant)
            int projLayer = LayerMask.NameToLayer(projectilesLayerName);
            if (projLayer != -1) go.layer = projLayer;

            if (arrowSprite != null)
            {
                // Enfant "Sprite" couché à plat
                var child = new GameObject("Sprite");
                child.transform.SetParent(go.transform, false);
                child.transform.localPosition = new Vector3(0f, spriteYOffset, 0f);
                child.transform.localRotation = Quaternion.Euler(-90f, 180f, 0f); // à plat (XZ)

                var sr = child.AddComponent<SpriteRenderer>();
                sr.sprite = arrowSprite;
                sr.sortingLayerName = spriteSortingLayer;
                sr.sortingOrder = spriteOrderInLayer;
                sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                sr.receiveShadows = false;

                // Fit largeur ? cellSize * spriteScale (en conservant le ratio)
                float w = sr.sprite != null ? sr.sprite.bounds.size.x : 1f;
                if (w <= 0f) w = 1f;
                float s = (cellSize * spriteScale) / w;
                child.transform.localScale = new Vector3(s, s, 1f);

                if (projLayer != -1) child.layer = projLayer;
            }
            else
            {
                // Fallback visuel cube si aucun sprite n'est assigné
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = "FallbackCube";
                cube.transform.SetParent(go.transform, false);
                cube.transform.localPosition = Vector3.zero;

                float sx = arrowThickness * cellSize;
                float sy = arrowThickness * cellSize;
                float sz = arrowLengthInCell * cellSize;
                cube.transform.localScale = new Vector3(sx, sy, sz);

                var col = cube.GetComponent<Collider>(); if (col) Destroy(col);
                var mr = cube.GetComponent<MeshRenderer>();
                if (mr)
                {
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    mr.receiveShadows = false;
                    if (arrowMaterial) mr.sharedMaterial = arrowMaterial;
                }
                if (projLayer != -1) cube.layer = projLayer;
            }

            // Logique inchangée
            var ap = go.AddComponent<ArrowProjectile>();
            ap.Init(
                dir, spec.arrowSpeed, cellSize, obstaclesMask,
                levelData, hero, _nmes, resetManager
            );
        }

        // --- Reset : on réarme tout (pour un reset de niveau complet) ---
        public void ResetToInitial()
        {
            for (int i = 0; i < _runtime.Length; i++)
            {
                _runtime[i].armed = true;
                _runtime[i].nextReadyTime = 0f;
            }

            _lastHeroCell = hero.GridPos;
            if (_nmes != null)
            {
                foreach (var n in _nmes)
                    if (n != null)
                        _lastNmeCell[n] = n.GridPos;
            }

            if (_tileVisuals != null)
            {
                foreach (var kv in _tileVisuals)
                    if (kv.Value != null) kv.Value.ResetVisual();
            }

            // on peut aussi clean d’éventuelles flèches résiduelles :
            // (optionnel) détruit tous les GameObjects "Arrow_*" sous ce LevelRoot
            // -> pas indispensable si on réinitialise toujours la scène proprement.
        }

        private System.Collections.IEnumerator RearmTile(TrapTileVisual tile, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (tile != null) tile.ShowThenRelease();
        }
    }
}
