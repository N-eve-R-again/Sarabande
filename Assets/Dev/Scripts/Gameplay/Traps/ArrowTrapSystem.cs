// FILE: Assets/Dev/Scripts/Traps/ArrowTrapSystem.cs
//
// Rôle (résumé)
// - Gère les pièges à flèches décrits dans LevelData.arrowTraps.
// - Déclenche lorsqu’un acteur (Héros ou NME) entre sur la cellule “trigger”.
// - Chaque piège peut lancer une ou plusieurs émissions de flèches (fixes et/ou répétitives).
// - Option de liaison à une DiscoSequence (les pièges liés ne se déclenchent que pendant la Disco).
// - Gère l’audio, un visuel de dalle pressée (TrapTileVisual) et le reset complet.
// - Expose SpawnArrow(startCell, travelDir, speed) pour instancier un projectile ArrowProjectile.
//
// Invariants
// - AUCUN renommage de champs sérialisés / méthodes publiques / signatures.
// - Logique identique à l’originale ; uniquement commentaires et renommages **locaux** pour clarté.

using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Sarabande.Levels;
using Sarabande.Player;
using Sarabande.NME;
using Sarabande.Core;   // EdgeDirection
using Sarabande.Traps;
using static Sarabande.Core.GridUtils;
using ArrowEmission = Sarabande.Levels.LevelData.ArrowEmission;

namespace Sarabande.Traps
{
    /// <summary>
    /// Système de dalles qui déclenchent des flèches selon des émissions (timers fixes et/ou répétitifs),
    /// avec réarmement optionnel, audio/FX et intégration LevelContext.
    /// </summary>
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

        // ????????????? Runtime caches ?????????????

        private NMEController[] _nmes;

        private struct TrapRuntime
        {
            public bool armed;           // true = prêt à tirer
            public float nextReadyTime;  // time à partir duquel on redevient 'armed'
        }
        private TrapRuntime[] _runtime;

        private Vector2Int _lastHeroCell;
        private readonly Dictionary<NMEController, Vector2Int> _lastNmeCell = new();

        // index de trap ? liste des coroutines d’émission actives (pour pouvoir les stopper)
        private readonly Dictionary<int, List<Coroutine>> _trapCo = new();

        // Parent pour ranger les FX runtime dans la hiérarchie
        private Transform _fxParent;

        // ?????????????????????????????????????????????????????????????????????????????
        // Unity lifecycle
        // ?????????????????????????????????????????????????????????????????????????????

        private void Start()
        {
            if (levelData == null || hero == null)
            {
                Debug.LogError("[ArrowTrapSystem] LevelData ou Hero manquant.");
                enabled = false;
                return;
            }

            // Racine pour les FX
            var fxRoot = new GameObject("FX_Runtime");
            _fxParent = fxRoot.transform;
            _fxParent.SetParent(transform, false);

            _nmes = FindObjectsByType<NMEController>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            int count = levelData.arrowTraps?.Count ?? 0;
            _runtime = new TrapRuntime[count];

            // Cache des dalles visuelles (si présentes dans la scène)

            // Init armement
            for (int i = 0; i < count; i++)
            {
                _runtime[i].armed = true;
                _runtime[i].nextReadyTime = 0f;
            }

            // Mémoires de cellule pour déclenchement “à l’entrée”
            _lastHeroCell = hero.GridPos;
            if (_nmes != null)
                foreach (var n in _nmes)
                    if (n != null) _lastNmeCell[n] = n.GridPos;

            RefreshNMECache(); // première passe sûre
        }

        // ?????????????????????????????????????????????????????????????????????????????
        // Déclenchements & émissions
        // ?????????????????????????????????????????????????????????????????????????????

        /// <summary>
        /// Tente de déclencher tous les pièges qui ont <paramref name="enteredCell"/> pour cellule trigger.
        /// Respecte l’état d’armement et la liaison Disco.
        /// </summary>

        /// <summary>
        /// Déclenche un piège : armement ? visuel ? audio ? planification des émissions.
        /// </summary>
        private void FireTrap(int index, LevelData.ArrowTrapSpec spec)
        {
            // Armement (désarmé + éventuel réarmement programmé)
            _runtime[index].armed = false;
            if (spec.canRearm) _runtime[index].nextReadyTime = Time.time + spec.rearmDelay;

            // Visuel de la dalle
            var triggerCell = new Vector2Int(spec.triggerCell.x, spec.triggerCell.z);


            // SFX “clic”
            var triggerPosWorld = Center(triggerCell, cellSize) + Vector3.up * 0.02f;
            Sarabande.Audio.AudioHub.I?.PlaySFXAt(
                clickTriggerClip,
                triggerPosWorld,
                clickVolume,
                spatialBlend,
                minDistance,
                maxDistance
            );

            // Bruit de gameplay (pour NME etc.)
            Sarabande.Core.NoiseSystem.Emit(triggerCell);

            // On arrête toute émission encore en cours pour CE trap
            StopTrapEmissions(index);

            // Résout la liste d’émissions effective (fallback si vide)
            var emissions = ResolveEmissions(spec);

            // Lance les routines d’émission pour chaque entrée
            for (int e = 0; e < emissions.Count; e++)
            {
                var em = emissions[e];
                if (!ValidateEmission(em, index, e))
                    continue; // skip propre si la spec est invalide

                // Timings fixes
                if (em.shotTimes != null && em.shotTimes.Any(t => t >= 0f))
                {
                    var coFixed = StartCoroutine(EmitFixedTimesRoutine(em));
                    RegisterTrapCo(index, coFixed);
                }

                // Pattern répétitif
                if (em.repeat && (em.repeatCount > 0 || em.repeatDuration > 0f))
                {
                    var coRepeat = StartCoroutine(EmitRepeatRoutine(em));
                    RegisterTrapCo(index, coRepeat);
                }
            }
        }

        /// <summary>
        /// Construit la liste des émissions à partir du spec, avec fallback si 'emissions' est vide.
        /// </summary>
        private List<ArrowEmission> ResolveEmissions(LevelData.ArrowTrapSpec spec)
        {
            if (spec.emissions != null && spec.emissions.Count > 0)
                return spec.emissions;

            // Fallback : une émission immédiate avec params de base
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

        /// <summary>Enregistre une coroutine d’émission pour pouvoir la stopper à la volée.</summary>
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

        /// <summary>Stoppe et purge toutes les émissions en cours pour un piège donné.</summary>
        private void StopTrapEmissions(int index)
        {
            if (_trapCo.TryGetValue(index, out var list))
            {
                foreach (var c in list) if (c != null) StopCoroutine(c);
                list.Clear();
            }
        }

        /// <summary>
        /// Valide qu’une émission possède des paramètres exploitables (cellule, vitesse, planning).
        /// En cas d’erreur, log explicite avec indices (trap, emission).
        /// </summary>
        private bool ValidateEmission(ArrowEmission em, int trapIndex, int emissionIndex)
        {
            if (levelData == null) return false;

            int w = levelData.width;
            int h = levelData.height;
            var startCell = new Vector2Int(em.startCell.x, em.startCell.z);

            // 1) startCell dans la grille
            if (startCell.x < 0 || startCell.x >= w || startCell.y < 0 || startCell.y >= h)
            {
                Debug.LogError($"[ArrowTrapSystem] Missing spec for arrowtrap emission (trap {trapIndex}, emission {emissionIndex}): startCell {startCell} out of bounds.");
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

        /// <summary>Émet des tirs aux instants absolus fournis dans <c>shotTimes</c> (en secondes).</summary>
        private System.Collections.IEnumerator EmitFixedTimesRoutine(ArrowEmission em)
        {
            if (em.shotTimes == null || em.shotTimes.Count == 0) yield break;

            // Trier & convertir en deltas d’attente
            var times = em.shotTimes.Where(t => t >= 0f).OrderBy(t => t).ToList();
            if (times.Count == 0) yield break;

            float previousTime = 0f;
            foreach (var absolute in times)
            {
                float wait = Mathf.Max(0f, absolute - previousTime);
                if (wait > 0f) yield return new WaitForSeconds(wait);

                SpawnArrow(new Vector2Int(em.startCell.x, em.startCell.z), em.travelDir, (em.arrowSpeed > 0f ? em.arrowSpeed : 6f));
                previousTime = absolute;
            }
        }

        /// <summary>Émet un pattern répétitif (par compte ou par durée).</summary>
        private System.Collections.IEnumerator EmitRepeatRoutine(ArrowEmission em)
        {
            if (em.repeatStartDelay > 0f)
                yield return new WaitForSeconds(em.repeatStartDelay);

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
                float endTime = Time.time + em.repeatDuration;
                while (Time.time < endTime)
                {
                    SpawnArrow(startCell, em.travelDir, speed);
                    yield return new WaitForSeconds(Mathf.Max(0.01f, em.repeatInterval));
                }
            }
        }

        // ?????????????????????????????????????????????????????????????????????????????
        // Reset
        // ?????????????????????????????????????????????????????????????????????????????

        /// <summary>
        /// Réinitialise l’armement, l’état des dalles, les caches de positions et **stoppe toutes** les émissions planifiées.
        /// </summary>
        public void ResetToInitial()
        {
            // Armement & timers
            for (int i = 0; i < _runtime.Length; i++)
            {
                _runtime[i].armed = true;
                _runtime[i].nextReadyTime = 0f;
            }

            // Suivi des cellules (évite les “manqués” sur la première frame post-reset)
            _lastHeroCell = hero.GridPos;
            if (_nmes != null)
                foreach (var n in _nmes)
                    if (n != null) _lastNmeCell[n] = n.GridPos;

            // Dalles visuelles


            // Stoppe toutes les planifications en cours pour tous les pièges
            foreach (var kv in _trapCo)
            {
                var list = kv.Value;
                if (list == null) continue;
                foreach (var c in list) if (c != null) StopCoroutine(c);
                list.Clear();
            }
        }



        // ?????????????????????????????????????????????????????????????????????????????
        // Instanciation d’un projectile
        // ?????????????????????????????????????????????????????????????????????????????

        /// <summary>
        /// Crée un projectile flèche sur <paramref name="startCell"/> qui se déplace vers
        /// <paramref name="travelDir"/> à la vitesse <paramref name="speed"/>.
        /// Gère sprite/cube fallback, SFX de départ et injection des dépendances dans ArrowProjectile.
        /// </summary>
        public void SpawnArrow(Vector2Int startCell, Sarabande.Core.CardinalDirection travelDir, float speed)
        {
            Vector3 startPosWorld = Center(startCell, cellSize) + Vector3.up * 0.02f;
            Vector3 worldDir = DirToWorld(travelDir);

            var arrowGO = new GameObject($"Arrow_{startCell.x}_{startCell.y}_{travelDir}");
            arrowGO.transform.position = startPosWorld;
            arrowGO.transform.rotation = Quaternion.LookRotation(worldDir, Vector3.up);

            int projectilesLayer = LayerMask.NameToLayer(projectilesLayerName);
            if (projectilesLayer != -1) arrowGO.layer = projectilesLayer;

            if (arrowSprite != null)
            {
                // Affichage sprite billboard (Z vers l’avant)
                var spriteGO = new GameObject("Sprite");
                spriteGO.transform.SetParent(arrowGO.transform, false);
                spriteGO.transform.localPosition = new Vector3(0f, spriteYOffset, 0f);
                spriteGO.transform.localRotation = Quaternion.Euler(-90f, 180f, 0f);

                var spriteRenderer = spriteGO.AddComponent<SpriteRenderer>();
                spriteRenderer.sprite = arrowSprite;
                spriteRenderer.sortingLayerName = spriteSortingLayer;
                spriteRenderer.sortingOrder = spriteOrderInLayer;
                spriteRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                spriteRenderer.receiveShadows = false;

                // Mise à l’échelle en fonction de la taille de cellule
                float spriteWidth = (spriteRenderer.sprite != null) ? spriteRenderer.sprite.bounds.size.x : 1f;
                if (spriteWidth <= 0f) spriteWidth = 1f;
                float s = (cellSize * spriteScale) / spriteWidth;
                spriteGO.transform.localScale = new Vector3(s, s, 1f);

                if (projectilesLayer != -1) spriteGO.layer = projectilesLayer;
            }
            else
            {
                // Fallback cube si pas de sprite
                var fallbackCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                fallbackCube.name = "FallbackCube";
                fallbackCube.transform.SetParent(arrowGO.transform, false);
                fallbackCube.transform.localPosition = Vector3.zero;

                float sx = arrowThickness * cellSize;
                float sy = arrowThickness * cellSize;
                float sz = arrowLengthInCell * cellSize;
                fallbackCube.transform.localScale = new Vector3(sx, sy, sz);

                var colliderComponent = fallbackCube.GetComponent<Collider>();
                if (colliderComponent) Destroy(colliderComponent);

                var meshRenderer = fallbackCube.GetComponent<MeshRenderer>();
                if (meshRenderer)
                {
                    meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    meshRenderer.receiveShadows = false;
                    if (arrowMaterial) meshRenderer.sharedMaterial = arrowMaterial;
                }
                if (projectilesLayer != -1) fallbackCube.layer = projectilesLayer;
            }

            // SFX de départ
            Sarabande.Audio.AudioHub.I?.PlaySFXAt(
                bowReleaseClip,
                startPosWorld,
                releaseVolume,
                spatialBlend,
                minDistance,
                maxDistance
            );

            // Projectile (composant runtime)
            var ap = arrowGO.AddComponent<Sarabande.Traps.ArrowProjectile>();
            ap.Init(worldDir, speed, cellSize, obstaclesMask, levelData, hero, _nmes, resetManager);
            ap.InitAudio(hitLoveClip, hitVolume, spatialBlend, minDistance, maxDistance);
            ap.InitFx(hitLoveFxPrefab, _fxParent, fxLifetime, fxYOffset);
        }

        // ?????????????????????????????????????????????????????????????????????????????
        // Disco linkage (optionnel)
        // ?????????????????????????????????????????????????????????????????????????????

        /// <summary>Appelé à la mise en route de la DiscoSequence (réarme les traps liés).</summary>
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
                    // purge de sécurité : si ce trap avait des émissions encore actives (après restart)
                    StopTrapEmissions(i);
                }
            }
        }

        /// <summary>Appelé à l’arrêt de la DiscoSequence.</summary>
        public void OnDiscoStop()
        {
            _discoRunning = false;
        }

        // ?????????????????????????????????????????????????????????????????????????????
        // Caches NME & LevelContext
        // ?????????????????????????????????????????????????????????????????????????????

        /// <summary>Reconstruit le cache des NME et réinitialise le suivi de leur cellule.</summary>
        private void RefreshNMECache()
        {
            _nmes = FindObjectsByType<NMEController>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            _lastNmeCell.Clear();
            if (_nmes != null)
                foreach (var n in _nmes) if (n) _lastNmeCell[n] = n.GridPos;
        }

        /// <summary>Abonnement au LevelContext (si présent) + init immédiate.</summary>
        private void AttachContext()
        {
            if (!useLevelContext) return;
            if (!levelContext) levelContext = GetComponentInParent<Sarabande.Core.LevelContext>();
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
