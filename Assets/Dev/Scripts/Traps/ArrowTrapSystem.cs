using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Sarabande.Levels;
using Sarabande.Player;
using Sarabande.NME;
using Sarabande.Core;   // <-- pour EdgeDirection
using Sarabande.Traps;
using static Sarabande.Core.GridUtils;
using ArrowEmission = Sarabande.Levels.LevelData.ArrowEmission;

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

        [SerializeField] private Sarabande.Disco.DiscoSequenceSystem disco; // assigner dans l’Inspector (ou auto-find)
        private bool _discoRunning = false;

        private NMEController[] _nmes;
        private Dictionary<Vector2Int, TrapTileVisual> _tileVisuals;
        private struct TrapRuntime { public bool armed; public float nextReadyTime; }
        private TrapRuntime[] _runtime;

        private Vector2Int _lastHeroCell;
        private readonly Dictionary<NMEController, Vector2Int> _lastNmeCell = new();

        private readonly Dictionary<int, List<Coroutine>> _trapCo = new();

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

            RefreshNMECache();
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

                if (spec.linkToDisco && !_discoRunning)
                    continue;

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

            var posTrig = Center(triggerCell, cellSize) + Vector3.up * 0.02f;

            Sarabande.Audio.AudioHub.I?.PlaySFXAt(
    clickTriggerClip,
    posTrig,
    clickVolume,
    spatialBlend,
    minDistance,
    maxDistance
);
            Sarabande.Core.NoiseSystem.Emit(triggerCell);

            // NEW: stoppe toute émission encore en cours pour CE trap
            StopTrapEmissions(index);

            // NEW: résout la/les émissions à partir du spec (fallback si liste vide)
            var emissions = ResolveEmissions(spec);

            // NEW: lance les coroutines d’émission pour chaque entrée
            for (int e = 0; e < emissions.Count; e++)
            {
                var em = emissions[e];
                if (!ValidateEmission(em, index, e))
                    continue; // on SKIP cette émission si elle est invalide

                // Tir(s) à horaires fixes
                if (em.shotTimes != null && em.shotTimes.Any(t => t >= 0f))
                {
                    var coA = StartCoroutine(EmitFixedTimesRoutine(em));
                    RegisterTrapCo(index, coA);
                }

                // Pattern répétitif seulement si repeat est correctement paramétré
                if (em.repeat && (em.repeatCount > 0 || em.repeatDuration > 0f))
                {
                    var coB = StartCoroutine(EmitRepeatRoutine(em));
                    RegisterTrapCo(index, coB);
                }
            }
        }

        private List<ArrowEmission> ResolveEmissions(LevelData.ArrowTrapSpec spec)
        {
            if (spec.emissions != null && spec.emissions.Count > 0)
                return spec.emissions;

            return new List<ArrowEmission>
    {
        new ArrowEmission
        {
            startCell  = spec.startCell,
            travelDir  = spec.travelDir,
            arrowSpeed = (spec.arrowSpeed > 0f ? spec.arrowSpeed : 6f),
            shotTimes  = new List<float> { 0f },
            repeat     = false
        }
    };
        }

        private void RegisterTrapCo(int index, Coroutine co)
        {
            if (co == null) return;
            if (!_trapCo.TryGetValue(index, out var list))
            {
                list = new List<Coroutine>();
                _trapCo[index] = list;
            }
            list.Add(co);
        }

        private void StopTrapEmissions(int index)
        {
            if (_trapCo.TryGetValue(index, out var list))
            {
                foreach (var c in list) if (c != null) StopCoroutine(c);
                list.Clear();
            }
        }

        private bool ValidateEmission(ArrowEmission em, int trapIndex, int emissionIndex)
        {
            if (levelData == null) return false;

            int w = levelData.width;
            int h = levelData.height;
            var sc = new Vector2Int(em.startCell.x, em.startCell.z);

            // 1) startCell dans la grille
            if (sc.x < 0 || sc.x >= w || sc.y < 0 || sc.y >= h)
            {
                Debug.LogError($"[ArrowTrapSystem] Missing spec for arrowtrap emission (trap {trapIndex}, emission {emissionIndex}): startCell {sc} out of bounds.");
                return false;
            }

            // 2) vitesse valide
            if (em.arrowSpeed <= 0f)
            {
                Debug.LogError($"[ArrowTrapSystem] Missing spec for arrowtrap emission (trap {trapIndex}, emission {emissionIndex}): arrowSpeed must be > 0.");
                return false;
            }

            // 3) planning de tir valide
            bool hasFixed = em.shotTimes != null && em.shotTimes.Any(t => t >= 0f);
            bool hasRepeat = em.repeat && (em.repeatCount > 0 || em.repeatDuration > 0f);

            if (!hasFixed && !hasRepeat)
            {
                Debug.LogError($"[ArrowTrapSystem] Missing spec for arrowtrap emission (trap {trapIndex}, emission {emissionIndex}): define shotTimes >= 0 OR set repeat with repeatCount > 0 or repeatDuration > 0.");
                return false;
            }

            return true;
        }


        private System.Collections.IEnumerator EmitFixedTimesRoutine(ArrowEmission em)
        {
            if (em.shotTimes == null || em.shotTimes.Count == 0) yield break;

            // trier & convertir en deltas
            var times = em.shotTimes.Where(t => t >= 0f).OrderBy(t => t).ToList();
            if (times.Count == 0) yield break;

            float prev = 0f;
            foreach (var t in times)
            {
                float wait = Mathf.Max(0f, t - prev);
                if (wait > 0f) yield return new WaitForSeconds(wait);
                SpawnArrow(new Vector2Int(em.startCell.x, em.startCell.z), em.travelDir, (em.arrowSpeed > 0f ? em.arrowSpeed : 6f));
                prev = t;
            }
        }

        private System.Collections.IEnumerator EmitRepeatRoutine(ArrowEmission em)
        {
            if (em.repeatStartDelay > 0f) yield return new WaitForSeconds(em.repeatStartDelay);

            var startCell = new Vector2Int(em.startCell.x, em.startCell.z);
            float speed = (em.arrowSpeed > 0f ? em.arrowSpeed : 6f);

            if (em.repeatCount > 0)
            {
                for (int i = 0; i < em.repeatCount; i++)
                {
                    SpawnArrow(startCell, em.travelDir, speed);
                    if (i < em.repeatCount - 1)
                        yield return new WaitForSeconds(Mathf.Max(0.01f, em.repeatInterval));
                }
                yield break;
            }

            if (em.repeatDuration > 0f)
            {
                float tEnd = Time.time + em.repeatDuration;
                while (Time.time < tEnd)
                {
                    SpawnArrow(startCell, em.travelDir, speed);
                    yield return new WaitForSeconds(Mathf.Max(0.01f, em.repeatInterval));
                }
                yield break;
            }

            yield break;
        }

        // --- ResetToInitial : ajouter l’arrêt des émissions programmées ---
        public void ResetToInitial()
        {
            // (logique existante)
            for (int i = 0; i < _runtime.Length; i++) { _runtime[i].armed = true; _runtime[i].nextReadyTime = 0f; }
            _lastHeroCell = hero.GridPos;
            if (_nmes != null) foreach (var n in _nmes) if (n != null) _lastNmeCell[n] = n.GridPos;
            if (_tileVisuals != null) foreach (var kv in _tileVisuals) if (kv.Value != null) kv.Value.ResetVisual();

            // NEW: stoppe toutes les planifications en cours
            foreach (var kv in _trapCo)
            {
                var list = kv.Value;
                if (list == null) continue;
                foreach (var c in list) if (c != null) StopCoroutine(c);
                list.Clear();
            }
        }

        private System.Collections.IEnumerator RearmTile(TrapTileVisual tile, float delay)
        { yield return new WaitForSeconds(delay); if (tile != null) tile.ShowThenRelease(); }

        public void SpawnArrow(Vector2Int startCell, Sarabande.Core.EdgeDirection travelDir, float speed)
        {
            Vector3 startPos = Center(startCell, cellSize) + Vector3.up * 0.02f;
            Vector3 dir = DirToWorld(travelDir);

            var go = new GameObject($"Arrow_{startCell.x}_{startCell.y}_{travelDir}");
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



            // SFX de départ
            Sarabande.Audio.AudioHub.I?.PlaySFXAt(
    bowReleaseClip,
    startPos,
    releaseVolume,
    spatialBlend,
    minDistance,
    maxDistance
);

            // Projectile
            var ap = go.AddComponent<Sarabande.Traps.ArrowProjectile>();
            ap.Init(dir, speed, cellSize, obstaclesMask, levelData, hero, _nmes, resetManager);
            ap.InitAudio(hitLoveClip, hitVolume, spatialBlend, minDistance, maxDistance);
            ap.InitFx(hitLoveFxPrefab, _fxParent, fxLifetime, fxYOffset);
        }

        public void OnDiscoStart()
        {
            _discoRunning = true;

            // Réarmer tous les traps liés à la Disco, peu importe leur état/canRearm
            if (levelData == null || _runtime == null) return;
            for (int i = 0; i < _runtime.Length && i < (levelData.arrowTraps?.Count ?? 0); i++)
            {
                var spec = levelData.arrowTraps[i];
                if (spec.linkToDisco)
                {
                    _runtime[i].armed = true;
                    _runtime[i].nextReadyTime = 0f;
                    // sécurité : si ce trap avait des émissions programmées qui traînent (après un restart), on purge
                    StopTrapEmissions(i);
                }
            }
        }

        public void OnDiscoStop()
        {
            _discoRunning = false;
        }

        private void RefreshNMECache()
        {
            _nmes = FindObjectsByType<NMEController>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            // remet à jour le suivi des cases pour éviter un “manqué” au premier tick
            _lastNmeCell.Clear();
            if (_nmes != null)
                foreach (var n in _nmes) if (n) _lastNmeCell[n] = n.GridPos;
        }

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
        private void OnEnable()
        {
            AttachContext();
            Sarabande.NME.NMESpawnSystem.AfterRebuild += RefreshNMECache;
        }
        private void OnDisable() 
        {
            Sarabande.NME.NMESpawnSystem.AfterRebuild -= RefreshNMECache;
            DetachContext(); 
        }
#if UNITY_EDITOR
        private void OnValidate() { if (!Application.isPlaying) AttachContext(); }
#endif
    }
}
