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
    public class ArrowTrapSystem : MonoBehaviour, Sarabande.Core.IResettable
    {
        [Header("Data & Refs")]
        [SerializeField] private bool useLevelContext = true;
        [SerializeField] private Sarabande.Core.LevelContext levelContext;
        [SerializeField, HideInInspector] private Sarabande.Levels.LevelData levelData;
        [SerializeField, Min(0.001f)] private float cellSize = 1f;
        [SerializeField] private HeroController hero;
        [SerializeField] private Sarabande.Core.ResetManager resetManager;

        [Header("Visuals")]
        [SerializeField] private Material arrowMaterial;
        [SerializeField, Min(0.02f)] private float arrowThickness = 0.08f;
        [SerializeField, Min(0.1f)] private float arrowLengthInCell = 0.7f;

        [Header("Sprite (projectile)")]
        [SerializeField] private Sprite arrowSprite; // sprite "North"
        [SerializeField, Range(0.1f, 2f)] private float spriteScale = 0.8f;
        [SerializeField] private float spriteYOffset = 0.02f;
        [SerializeField] private string spriteSortingLayer = "Default";
        [SerializeField] private int spriteOrderInLayer = 50;

        [Header("Collision")]
        [SerializeField] private LayerMask obstaclesMask;

        [Header("Layer (optionnel)")]
        [SerializeField] private string projectilesLayerName = "Projectiles";

        [Header("Audio")]
        [SerializeField] private AudioClip clickTriggerClip;  // clic dalle
        [SerializeField] private AudioClip bowReleaseClip;    // corde
        [SerializeField] private AudioClip hitLoveClip;       // pouf love (impact)
        [SerializeField, Range(0f, 1f)] private float clickVolume = 0.9f;
        [SerializeField, Range(0f, 1f)] private float releaseVolume = 0.9f;
        [SerializeField, Range(0f, 1f)] private float hitVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float spatialBlend = 1f; // 3D
        [SerializeField] private float minDistance = 2f;
        [SerializeField] private float maxDistance = 18f;

        [Header("FX")]
        [Tooltip("Prefab de FX (ParticleSystem/VFX Graph) pour l’impact cœur.")]
        [SerializeField] private GameObject hitLoveFxPrefab;
        [Tooltip("Durée de vie fallback si le prefab n’auto-détruit pas.")]
        [SerializeField, Min(0.1f)] private float fxLifetime = 1.5f;
        [SerializeField] private float fxYOffset = 0.05f;

        private NMEController[] _nmes;
        private Dictionary<Vector2Int, TrapTileVisual> _tileVisuals;
        private struct TrapRuntime { public bool armed; public float nextReadyTime; }
        private TrapRuntime[] _runtime;

        private Vector2Int _lastHeroCell;
        private readonly Dictionary<NMEController, Vector2Int> _lastNmeCell = new();

        // Parent propre pour retrouver tous les FX rapidement dans la hiérarchie
        private Transform _fxParent;

        private void Start()
        {
            if (levelData == null || hero == null)
            {
                Debug.LogError("[ArrowTrapSystem] LevelData ou Hero manquant.");
                enabled = false;
                return;
            }

            // Dossier pour FX runtime (pour “où le trouver après”)
            var fxRoot = new GameObject("FX_Runtime");
            _fxParent = fxRoot.transform;
            _fxParent.SetParent(transform, false);

            _nmes = FindObjectsByType<NMEController>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            int count = levelData.arrowTraps?.Count ?? 0;
            _runtime = new TrapRuntime[count];
            _tileVisuals = new Dictionary<Vector2Int, TrapTileVisual>();
            var tiles = FindObjectsByType<TrapTileVisual>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var tv in tiles) if (tv != null) _tileVisuals[tv.Cell] = tv;

            for (int i = 0; i < count; i++) { _runtime[i].armed = true; _runtime[i].nextReadyTime = 0f; }

            _lastHeroCell = hero.GridPos;
            if (_nmes != null) foreach (var n in _nmes) if (n != null) _lastNmeCell[n] = n.GridPos;
        }

        private void Update()
        {
            for (int i = 0; i < _runtime.Length; i++)
            {
                if (!_runtime[i].armed && Time.time >= _runtime[i].nextReadyTime)
                { var spec = levelData.arrowTraps[i]; if (spec.canRearm) _runtime[i].armed = true; }
            }

            var heroCell = hero.GridPos;
            if (heroCell != _lastHeroCell) { TryTriggerAtCell(heroCell); _lastHeroCell = heroCell; }

            if (_nmes != null)
            {
                foreach (var n in _nmes)
                {
                    if (n == null) continue;
                    var cell = n.GridPos;
                    if (_lastNmeCell.TryGetValue(n, out var prev))
                    {
                        if (cell != prev) { TryTriggerAtCell(cell); _lastNmeCell[n] = cell; }
                    }
                    else _lastNmeCell[n] = cell;
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
                    FireTrap(i, spec);
            }
        }

        private void FireTrap(int index, LevelData.ArrowTrapSpec spec)
        {
            _runtime[index].armed = false;
            if (spec.canRearm) _runtime[index].nextReadyTime = Time.time + spec.rearmDelay;

            var triggerCell = new Vector2Int(spec.triggerCell.x, spec.triggerCell.z);
            if (_tileVisuals != null && _tileVisuals.TryGetValue(triggerCell, out var tile))
            {
                tile.PressAndHide();
                if (spec.canRearm) StartCoroutine(RearmTile(tile, spec.rearmDelay));
            }

            PlayOneShotAt(clickTriggerClip, Center(triggerCell, cellSize) + Vector3.up * 0.02f, clickVolume);
            Sarabande.Core.NoiseSystem.Emit(triggerCell);

            Vector3 startPos = Center(new Vector2Int(spec.startCell.x, spec.startCell.z), cellSize) + Vector3.up * 0.02f;
            Vector3 dir = DirToWorld(spec.travelDir);

            var go = new GameObject($"Arrow_{index}");
            go.transform.position = startPos;
            go.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);

            int projLayer = LayerMask.NameToLayer(projectilesLayerName);
            if (projLayer != -1) go.layer = projLayer;

            if (arrowSprite != null)
            {
                var child = new GameObject("Sprite");
                child.transform.SetParent(go.transform, false);
                child.transform.localPosition = new Vector3(0f, spriteYOffset, 0f);
                child.transform.localRotation = Quaternion.Euler(-90f, 180f, 0f);

                var sr = child.AddComponent<SpriteRenderer>();
                sr.sprite = arrowSprite;
                sr.sortingLayerName = spriteSortingLayer;
                sr.sortingOrder = spriteOrderInLayer;
                sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                sr.receiveShadows = false;

                float w = sr.sprite != null ? sr.sprite.bounds.size.x : 1f;
                if (w <= 0f) w = 1f;
                float s = (cellSize * spriteScale) / w;
                child.transform.localScale = new Vector3(s, s, 1f);

                if (projLayer != -1) child.layer = projLayer;
            }
            else
            {
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

            PlayOneShotAt(bowReleaseClip, startPos, releaseVolume);

            var ap = go.AddComponent<ArrowProjectile>();
            ap.Init(dir, spec.arrowSpeed, cellSize, obstaclesMask, levelData, hero, _nmes, resetManager);
            ap.InitAudio(hitLoveClip, hitVolume, spatialBlend, minDistance, maxDistance);
            ap.InitFx(hitLoveFxPrefab, _fxParent, fxLifetime, fxYOffset);
        }

        public void ResetToInitial()
        {
            for (int i = 0; i < _runtime.Length; i++) { _runtime[i].armed = true; _runtime[i].nextReadyTime = 0f; }

            _lastHeroCell = hero.GridPos;
            if (_nmes != null) foreach (var n in _nmes) if (n != null) _lastNmeCell[n] = n.GridPos;

            if (_tileVisuals != null) foreach (var kv in _tileVisuals) if (kv.Value != null) kv.Value.ResetVisual();
        }

        private System.Collections.IEnumerator RearmTile(TrapTileVisual tile, float delay)
        { yield return new WaitForSeconds(delay); if (tile != null) tile.ShowThenRelease(); }

        private void AttachContext()
        {
            if (!useLevelContext) return;
            if (!levelContext) levelContext = GetComponentInParent<Sarabande.Core.LevelContext>();
            if (levelContext != null)
            {
                levelContext.LevelDataChanged += HandleContextLevelDataChanged;
                HandleContextLevelDataChanged(levelContext.LevelData);
            }
            else Debug.LogWarning($"[{GetType().Name}] Aucun LevelContext parent trouvé.");
        }
        private void DetachContext() { if (levelContext != null) levelContext.LevelDataChanged -= HandleContextLevelDataChanged; }
        private void HandleContextLevelDataChanged(Sarabande.Levels.LevelData ld)
        {
            if (levelData == ld) return;
            levelData = ld;
#if UNITY_EDITOR
            if (!Application.isPlaying) UnityEditor.EditorUtility.SetDirty(this);
#endif
        }
        private void OnEnable() { AttachContext(); }
        private void OnDisable() { DetachContext(); }
#if UNITY_EDITOR
        private void OnValidate() { if (!Application.isPlaying) AttachContext(); }
#endif

        private void PlayOneShotAt(AudioClip clip, Vector3 pos, float vol)
        {
            if (!clip) return;
            var go = new GameObject("SFX_ArrowTrap_OneShot");
            go.transform.position = pos;
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = false;
            src.clip = clip;
            src.volume = Mathf.Clamp01(vol);
            src.spatialBlend = spatialBlend;
            src.minDistance = minDistance;
            src.maxDistance = maxDistance;
            src.Play();
            Destroy(go, clip.length + 0.1f);
        }
    }
}
