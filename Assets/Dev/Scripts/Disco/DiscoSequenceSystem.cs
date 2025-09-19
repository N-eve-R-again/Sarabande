// FILE: Assets/DEV/Scripts/Disco/DiscoSequenceSystem.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Sarabande.Core;
using Sarabande.Levels;
using Sarabande.Player;

namespace Sarabande.Disco
{
    public class DiscoSequenceSystem : MonoBehaviour, IResettable
    {
        [Header("Data & Refs")]
        [SerializeField] private HeroController hero;
        [SerializeField] private DiscoTilesVisuals visuals; // si null, on prend GetComponent<DiscoTilesVisuals>()
        [SerializeField] private bool useLevelContext = true;
        [SerializeField] private Sarabande.Core.LevelContext levelContext;
        [SerializeField, HideInInspector] private Sarabande.Levels.LevelData levelData;

        [Header("Test/Debug")]
        [SerializeField] private int sequenceIndex = 0;
        [SerializeField] private bool autoStartOnPlay = false;

        [Header("Events")]
        public UnityEvent onSequenceStart;
        public UnityEvent onSequenceSuccess;
        public UnityEvent onSequenceFail;
      
        [Header("Audio")]
        [Tooltip("Son montant qui doit finir pile au tick. On démarre à (clip.length - tempsRestant).")]
        [SerializeField] private AudioClip progressClip;
        [SerializeField] private AudioClip stepDingClip;
        [SerializeField] private AudioClip successJingleClip;
        [SerializeField, Range(0f, 1f)] private float progressVolume = 0.9f;
        [SerializeField, Range(0f, 1f)] private float dingVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float successVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float spatialBlend = 0f; // 0 = 2D (UI-like), 1 = 3D
        [SerializeField] private float minDistance = 1f;
        [SerializeField] private float maxDistance = 15f;

        [Header("Next-core growth")]
        // 0.20 = 20% de la dalle ; 0.80 = 80% de la dalle
        [SerializeField, Range(0f, 1f)] private float nextCoreMinScale = 0.20f;
        [SerializeField, Range(0f, 1f)] private float nextCoreMaxScale = 0.80f;
        // Petite pause avant le tick : pendant ces X secondes, le core n’augmente plus (reste à 80%)
        [SerializeField, Min(0f)] private float nextCoreSnapLeadSeconds = 0.08f;

        // Debug
        [SerializeField] private bool debugDisco = true;


        // --- runtime ---
        private List<Vector2Int> _cells;    // cellules de la séquence active
        private List<float> _durations;     // secondes par étape (alignée à _cells)
        private int _step = -1;             // index de l’étape en cours (0..N-1)
        private float _deadline = 0f;       // Time.time auquel on “clôture” cette étape
        private bool _running = false;

        // Audio runtime
        private AudioSource _progressSrc;   // source dédiée au son montant (offsettable)
        private int _progressForStep = -1;  // garde-fou pour ne (re)lancer que quand on entre sur la tuile ON
        private bool _progressPlaying = false;

        // ----------
        private void Awake()
        {
            if (!visuals) visuals = GetComponent<DiscoTilesVisuals>();

            // NOTE: le LevelData est injecté via LevelContext en OnEnable/OnValidate.
            // On NE valide donc pas levelData ici.
        }

        private void Start()
        {
            // Validation retardée pour laisser LevelContext injecter levelData
            if (!hero || !visuals || (!levelData && useLevelContext))
            {
                Debug.LogError("[DiscoSequenceSystem] Références manquantes (LevelData/Hero/Visuals).");
                enabled = false; return;
            }

            if (autoStartOnPlay)
                StartSequence(sequenceIndex);
        }

        private void Update()
        {
            if (!_running) return;
            UpdateNextCoreGrowth();

            // 1) échec immédiat si Hero marche sur une tuile future (index > _step)
            var heroIdx = IndexOfCell(hero.GridPos);
            if (heroIdx >= 0 && heroIdx > _step)
            {
                if (debugDisco) Debug.Log($"[Disco][FAIL] future tile: step={_step} heroIdx={heroIdx} hero={hero.GridPos}");
                FailSequence();
                return;
            }

            // 1.b) gestion du son “progress” (joué SEULEMENT si on est sur la tuile ON courante)
            if (_step >= 0 && _step < _cells.Count)
            {
                bool heroOnCurrent = (hero.GridPos == _cells[_step]);
                if (heroOnCurrent)
                {
                    // Si on vient d'entrer sur la tuile ON pour CETTE étape -> on lance/recale le progress
                    if (_progressForStep != _step)
                        StartProgressForCurrentStep();
                }
                else
                {
                    // Hors tuile ON -> on coupe le progress
                    StopProgress();
                }
            }

            // 2) tick de fin d’étape
            if (Time.time >= _deadline)
            {
                if (hero.GridPos == _cells[_step])
                {
                    AdvanceStep();
                }
                else
                {
                    if (debugDisco) Debug.Log($"[Disco][FAIL] missed tick: step={_step} expected={_cells[_step]} hero={hero.GridPos} t={Time.time:0.000} deadline={_deadline:0.000}");
                    FailSequence();
                }
            }
        }

        // --- API ---
        [ContextMenu("Disco: Start sequence (sequenceIndex)")]
        public void StartSequenceContext() => StartSequence(sequenceIndex);

        public void StartSequence(int index)
        {
            if (levelData == null || levelData.discoSequences == null ||
                index < 0 || index >= levelData.discoSequences.Count)
            {
                Debug.LogWarning("[DiscoSequenceSystem] Index de séquence invalide.");
                return;
            }

            var spec = levelData.discoSequences[index];
            if (spec == null || spec.cells == null || spec.cells.Count == 0)
            {
                Debug.LogWarning("[DiscoSequenceSystem] Séquence vide.");
                return;
            }

            // prépare cellules & durées
            _cells = new List<Vector2Int>(spec.cells.Count);
            foreach (var v in spec.cells) _cells.Add(new Vector2Int(v.x, v.z));

            _durations = new List<float>(_cells.Count);
            for (int i = 0; i < _cells.Count; i++)
            {
                float s = (spec.stepSeconds != null && i < spec.stepSeconds.Count && spec.stepSeconds[i] > 0f)
                        ? spec.stepSeconds[i]
                        : Mathf.Max(0.05f, spec.defaultStepSeconds);
                _durations.Add(s);
            }

            // reset visuel & état
            visuals.SetAllOff();
            _step = 0;
            _running = true;
            _progressForStep = -1;
            StopProgress();

            // État initial : 0 = ON, 1 = NEXT, le reste OFF (mais visibles)
            visuals.SetState(_cells[0], DiscoTilesVisuals.State.On, PickBright());
            if (_cells.Count >= 2)
            {
                visuals.SetState(_cells[1], DiscoTilesVisuals.State.Next, PickBright());
                visuals.SetNextCoreFill(_cells[1], nextCoreMinScale); // 20% de la dalle au départ
            }

            _deadline = Time.time + _durations[0];

            onSequenceStart?.Invoke();

            // Si le héros est DÉJÀ sur la première tuile, on peut lancer le progress immédiatement
            if (hero && hero.GridPos == _cells[0])
                StartProgressForCurrentStep();
        }

        public void StopSequence()
        {
            _running = false;
            _progressForStep = -1;
            StopProgress();
        }

        // --- progression ---
        private void AdvanceStep()
        {
            // Étape _step vient d’être validée
            _step++;
            _progressForStep = -1;
            StopProgress();

            if (_step >= _cells.Count)
            {
                // tout validé
                _running = false;
                PlaySuccessJingle();
                onSequenceSuccess?.Invoke();
                return;
            }

            // Nouvelle étape en cours = _step
            visuals.SetState(_cells[_step], DiscoTilesVisuals.State.On, PickBright());

            int nextIdx = _step + 1;
            if (nextIdx < _cells.Count)
            {
                visuals.SetState(_cells[nextIdx], DiscoTilesVisuals.State.Next, PickBright());
                visuals.SetNextCoreFill(_cells[nextIdx], nextCoreMinScale); // repart à 20%
            }

            _deadline = Time.time + _durations[_step];

            // si le héros est déjà dessus, relance progress immédiatement
            if (hero && hero.GridPos == _cells[_step])
                StartProgressForCurrentStep();
        }

        private void FailSequence()
        {
            _running = false;
            _progressForStep = -1;
            StopProgress();

            if (visuals) visuals.ClearAllNextCores();

            visuals.SetAllOff();
            onSequenceFail?.Invoke();
        }
        private void UpdateNextCoreGrowth()
        {
            if (!_running || _step < 0 || _step >= _cells.Count) return;

            int nextIdx = _step + 1;
            if (nextIdx >= _cells.Count) return; // pas de NEXT à la dernière étape

            float stepTotal = _durations[_step];
            float growthDuration = Mathf.Max(0.01f, stepTotal - nextCoreSnapLeadSeconds);

            // temps écoulé dans l’étape en cours
            float elapsed = Mathf.Clamp(stepTotal - Mathf.Max(0f, _deadline - Time.time), 0f, growthDuration);
            float u = Mathf.Clamp01(elapsed / growthDuration);

            float frac = Mathf.Lerp(nextCoreMinScale, nextCoreMaxScale, u);   // 20% -> 80% de la dalle
            if (visuals) visuals.SetNextCoreFill(_cells[nextIdx], frac);      // fraction de dalle
        }

        // --- util ---
        private int IndexOfCell(Vector2Int c)
        {
            if (_cells == null) return -1;
            for (int i = 0; i < _cells.Count; i++)
                if (_cells[i] == c) return i;
            return -1;
        }

        private static Color PickBright()
        {
            float h = Random.value;
            return Color.HSVToRGB(h, 1f, 1f);
        }

        public void ResetToInitial()
        {
            // On capture si on était réellement en cours pour ne pas spammer l’event au boot
            bool wasRunning = _running;

            StopSequence();                 // coupe l’état interne + sons de progression
            if (visuals)                    // nettoie le visuel
            {
                visuals.ClearAllNextCores();
                visuals.SetAllOff();
            }

            // IMPORTANT : prévenir tout le monde que la disco s’arrête suite à un fail (reset)
            if (wasRunning)
                onSequenceFail?.Invoke();
        }

        // --- LevelContext wiring ---
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
                UnityEditor.EditorUtility.SetDirty(this);
#endif
            // Si besoin: re-init en live, ex: StopSequence(); visuals.SetAllOff();
        }

        private void OnEnable() { AttachContext(); }
        private void OnDisable() { DetachContext(); StopProgress(); }
#if UNITY_EDITOR
        private void OnValidate() { if (!Application.isPlaying) AttachContext(); }
#endif

        // ------------------ AUDIO HELPERS ------------------

        private void StartProgressForCurrentStep()
        {
            if (!_running || _step < 0 || _step >= _cells.Count) return;
            if (!progressClip) return;

            float remaining = Mathf.Max(0f, _deadline - Time.time);
            if (remaining <= 0f) return;

            // Source (créée/positionnée sur la tuile ON courante)
            EnsureProgressSourceAt(CellCenterWorld(_cells[_step]) + Vector3.up * 0.05f);

            // Calage temporel: on démarre "comme si" les premières secondes étaient déjà passées
            float startTime = Mathf.Clamp(progressClip.length - remaining, 0f, Mathf.Max(0f, progressClip.length - 0.01f));

            _progressSrc.clip = progressClip;
            _progressSrc.volume = progressVolume;
            _progressSrc.spatialBlend = spatialBlend;
            _progressSrc.minDistance = minDistance;
            _progressSrc.maxDistance = maxDistance;
            _progressSrc.time = startTime;
            _progressSrc.Play();

            _progressForStep = _step;
            _progressPlaying = true;
        }

        private void StopProgress()
        {
            if (_progressSrc && _progressSrc.isPlaying) _progressSrc.Stop();
            _progressPlaying = false;
        }

        private void PlayStepDing()
        {
            if (!stepDingClip) return;
            PlayOneShotAt(stepDingClip, CellCenterWorld(_cells[Mathf.Clamp(_step, 0, _cells.Count - 1)]), dingVolume);
        }

        private void PlaySuccessJingle()
        {
            if (!successJingleClip) return;
            Vector3 pos = (hero ? hero.WorldPos : transform.position);
            PlayOneShotAt(successJingleClip, pos, successVolume);
        }

        private void EnsureProgressSourceAt(Vector3 worldPos)
        {
            if (_progressSrc == null)
            {
                var go = new GameObject("SFX_DiscoProgress");
                go.transform.SetParent(transform, false);
                _progressSrc = go.AddComponent<AudioSource>();
                _progressSrc.playOnAwake = false;
                _progressSrc.loop = false; // le clip se termine au tick, pas besoin de loop
            }
            _progressSrc.transform.position = worldPos;
        }

        private void PlayOneShotAt(AudioClip clip, Vector3 pos, float volume)
        {
            if (!clip) return;
            var go = new GameObject("SFX_DiscoOneShot");
            go.transform.position = pos;
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = false;
            src.clip = clip;
            src.volume = volume;
            src.spatialBlend = spatialBlend;
            src.minDistance = minDistance;
            src.maxDistance = maxDistance;
            src.Play();
            Destroy(go, clip.length + 0.1f);
        }

        private Vector3 CellCenterWorld(Vector2Int cell)
        {
            // On demande la position au visuel si dispo (c’est précisément le centre monde).
            var t = visuals ? visuals.GetTile(cell) : null;
            if (t != null) return t.transform.position;
            // fallback approximatif (cellSize=1) si jamais :
            return new Vector3(cell.x + 0.5f, 0f, cell.y + 0.5f);
        }
    }
}
