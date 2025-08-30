using System.Collections.Generic;
using UnityEngine;
using Sarabande.Levels;
using Sarabande.Player;
using Sarabande.NME;
using Sarabande.Core;   // <-- pour EdgeDirection
using Sarabande.Traps;

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

            _nmes = FindObjectsOfType<NMEController>(true);

            int count = levelData.arrowTraps?.Count ?? 0;
            _runtime = new TrapRuntime[count];
            _tileVisuals = new System.Collections.Generic.Dictionary<Vector2Int, TrapTileVisual>();
            var tiles = FindObjectsOfType<TrapTileVisual>(true);
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
                // AVANT: tile.Press();
                // MAINTENANT:
                tile.PressAndHide();

                if (spec.canRearm)
                    StartCoroutine(RearmTile(tile, spec.rearmDelay));
            }

            // spawn flèche
            Vector3 startPos = GridCenter(new Vector2Int(spec.startCell.x, spec.startCell.z)) + Vector3.up * 0.02f;
            Vector3 dir = DirToVector(spec.travelDir);

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = $"Arrow_{index}";
            go.transform.position = startPos;
            go.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);

            // scale visuelle (longueur dans l'axe Z local)
            float sx = arrowThickness * cellSize;
            float sy = arrowThickness * cellSize;
            float sz = arrowLengthInCell * cellSize;
            go.transform.localScale = new Vector3(sx, sy, sz);

            // enlever le collider pour ne pas interférer
            var col = go.GetComponent<Collider>(); if (col) Destroy(col);

            // material
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                if (arrowMaterial) mr.sharedMaterial = arrowMaterial;
            }

            // layer projectiles (optionnel)
            int projLayer = LayerMask.NameToLayer(projectilesLayerName);
            if (projLayer != -1) go.layer = projLayer;

            // logique
            var ap = go.AddComponent<ArrowProjectile>();
            ap.Init(
                dir, spec.arrowSpeed, cellSize, obstaclesMask,
                levelData, hero, _nmes, resetManager
            );
        }

        private Vector3 GridCenter(Vector2Int c)
        {
            return new Vector3((c.x + 0.5f) * cellSize, 0f, (c.y + 0.5f) * cellSize);
        }

        private static Vector3 DirToVector(EdgeDirection dir)
        {
            return dir switch
            {
                EdgeDirection.North => new Vector3(0f, 0f, 1f),
                EdgeDirection.East => new Vector3(1f, 0f, 0f),
                EdgeDirection.South => new Vector3(0f, 0f, -1f),
                EdgeDirection.West => new Vector3(-1f, 0f, 0f),
                _ => Vector3.forward
            };
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
