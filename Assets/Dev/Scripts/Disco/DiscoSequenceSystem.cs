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
        [SerializeField] private LevelData levelData;
        [SerializeField] private HeroController hero;
        [SerializeField] private DiscoTilesVisuals visuals; // si null, on prend GetComponent<DiscoTilesVisuals>()

        [Header("Test/Debug")]
        [SerializeField] private int sequenceIndex = 0;     // laquelle on teste
        [SerializeField] private bool autoStartOnPlay = false;

        [Header("Events")]
        public UnityEvent onSequenceSuccess;
        public UnityEvent onSequenceFail;

        // ---------- AUDIO ----------
        [Header("Audio")]
        [SerializeField] private AudioClip stepDingClip;          // ding à chaque fin d’étape
        [SerializeField] private AudioClip sequenceSuccessClip;   // jingle fin de séquence
        [SerializeField] private AudioClip rampClip;              // son qui “monte” et doit finir au tick
        [SerializeField, Range(0f, 1f)] private float sfxVolume = 0.9f;
        [SerializeField, Range(0f, 1f)] private float rampVolume = 0.8f;

        [Tooltip("0 = 2D (mix constant), 1 = 3D (spatial). Laisse à 0 pour commencer.")]
        [SerializeField, Range(0f, 1f)] private float spatialBlend = 0f;
        [SerializeField] private float minDistance = 2f;
        [SerializeField] private float maxDistance = 20f;

        private AudioSource _sfx;   // pour ding + success (OneShot)
        private AudioSource _ramp;  // pour le son qui “monte” (calé sur le temps restant)

        // --- runtime ---
        private List<Vector2Int> _cells;   // cellules de la séquence active
        private List<float> _durations;    // secondes par étape (alignée à _cells)
        private int _step = -1;            // index de l’étape en cours (0..N-1)
        private float _deadline = 0f;      // Time.time auquel on “clôture” cette étape
        private bool _running = false;

        // latch: est-ce que le héros est sur la tuile ON courante (pour démarrer/stopper le ramp)
        private bool _heroOnCurrent = false;

        private void Awake()
        {
            if (!visuals) visuals = GetComponent<DiscoTilesVisuals>();
            if (!levelData || !hero || !visuals)
            {
                Debug.LogError("[DiscoSequenceSystem] Références manquantes (LevelData / Hero / Visuals).");
                enabled = false; return;
            }

            // --- AudioSources (créés au runtime ici) ---
            _sfx = gameObject.AddComponent<AudioSource>();
            _sfx.playOnAwake = false;
            _sfx.loop = false;
            _sfx.volume = sfxVolume;
            _sfx.spatialBlend = spatialBlend;
            _sfx.minDistance = minDistance;
            _sfx.maxDistance = maxDistance;

            _ramp = gameObject.AddComponent<AudioSource>();
            _ramp.playOnAwake = false;
            _ramp.loop = false;
            _ramp.volume = rampVolume;
            _ramp.spatialBlend = spatialBlend;
            _ramp.minDistance = minDistance;
            _ramp.maxDistance = maxDistance;
            _ramp.clip = rampClip;

            if (autoStartOnPlay)
                StartSequence(sequenceIndex);
        }

        private void Update()
        {
            if (!_running) return;

            // 1) échec immédiat si Hero marche sur une tuile future (index > _step)
            var heroIdx = IndexOfCell(hero.GridPos);
            if (heroIdx >= 0 && heroIdx > _step)
            {
                FailSequence(); // tuile non encore “ON” ? interdit
                return;
            }

            // 1b) gestion entrée/sortie de la tuile ON courante -> (re)calage du ramp
            if (_step >= 0 && _step < (_cells?.Count ?? 0))
            {
                bool nowOn = (hero.GridPos == _cells[_step]);
                if (nowOn && !_heroOnCurrent)
                    StartRampAlignedToRemaining();
                else if (!nowOn && _heroOnCurrent)
                    StopRamp();
                _heroOnCurrent = nowOn;
            }

            // 2) tick de fin d’étape
            if (Time.time >= _deadline)
            {
                // doit être sur la tuile ON courante au moment du tick
                if (hero.GridPos == _cells[_step])
                {
                    // Fin d’étape validée : on stoppe le ramp et on ding
                    StopRamp();
                    PlayDing();
                    AdvanceStep();
                }
                else
                {
                    FailSequence();
                }
            }
        }

        // --- API ---
        [ContextMenu("Disco: Start sequence (sequenceIndex)")]
        public void StartSequenceContext()
        {
            StartSequence(sequenceIndex);
        }

        public void StartSequence(int index)
        {
            if (levelData.discoSequences == null ||
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

            // État initial : 0 = ON, 1 = NEXT, le reste OFF (mais visibles)
            visuals.SetState(_cells[0], DiscoTilesVisuals.State.On, PickBright());
            if (_cells.Count >= 2)
                visuals.SetState(_cells[1], DiscoTilesVisuals.State.Next, PickBright());

            _deadline = Time.time + _durations[0];

            // Audio : si le héros est déjà sur la 1ère tuile ON, on démarre le ramp calé
            _heroOnCurrent = (hero && hero.GridPos == _cells[0]);
            if (_heroOnCurrent) StartRampAlignedToRemaining();
            else StopRamp();
        }

        public void StopSequence()
        {
            _running = false;
            StopRamp();
        }

        // --- progression ---
        private void AdvanceStep()
        {
            // Étape _step vient d’être validée. On passe à la suivante.
            _step++;

            if (_step >= _cells.Count)
            {
                // tout validé
                _running = false;
                PlaySuccess();
                onSequenceSuccess?.Invoke();
                return;
            }

            // Nouvelle étape en cours = _step
            // La tuile “NEXT” précédente (index == _step) devient “ON”.
            visuals.SetState(_cells[_step], DiscoTilesVisuals.State.On, PickBright());

            // La tuile suivante (index == _step + 1) devient “NEXT” (si existe).
            int nextIdx = _step + 1;
            if (nextIdx < _cells.Count)
                visuals.SetState(_cells[nextIdx], DiscoTilesVisuals.State.Next, PickBright());

            _deadline = Time.time + _durations[_step];

            // Le ramp ne repart que si le héros est déjà sur la nouvelle tuile ON
            _heroOnCurrent = (hero && hero.GridPos == _cells[_step]);
            if (_heroOnCurrent) StartRampAlignedToRemaining();
        }

        private void FailSequence()
        {
            _running = false;
            StopRamp();
            visuals.SetAllOff();
            onSequenceFail?.Invoke();
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
            // Couleur vive lisible (H en 0..1, S=1, V=1)
            float h = Random.value;
            return Color.HSVToRGB(h, 1f, 1f);
        }

        // Reset global (si tu reset le niveau)
        public void ResetToInitial()
        {
            StopSequence();
            StopRamp();
            if (visuals) visuals.SetAllOff();
        }

        // ---------- AUDIO helpers ----------
        private void PlayDing()
        {
            if (_sfx && stepDingClip) _sfx.PlayOneShot(stepDingClip, sfxVolume);
        }

        private void PlaySuccess()
        {
            if (_sfx && sequenceSuccessClip) _sfx.PlayOneShot(sequenceSuccessClip, sfxVolume);
        }

        private void StopRamp()
        {
            if (_ramp) _ramp.Stop();
        }

        /// <summary>
        /// Démarre le son “ramp” aligné pour qu’il FINISSE exactement à _deadline.
        /// - Si le clip est assez long: on saute à (clipLen - remaining).
        /// - Sinon: on ralentit (pitch < 1) pour l’étirer jusqu’à remaining.
        /// </summary>
        private void StartRampAlignedToRemaining()
        {
            if (!_ramp || !rampClip) return;

            float remaining = Mathf.Max(0f, _deadline - Time.time);
            if (remaining <= 0f)
            {
                _ramp.Stop();
                return;
            }

            _ramp.Stop();
            _ramp.clip = rampClip;
            _ramp.volume = rampVolume;

            float len = rampClip.length;

            if (len >= remaining)
            {
                // Pas besoin d’étirer: on démarre à "len - remaining"
                _ramp.pitch = 1f;
                float startTime = Mathf.Clamp(len - remaining, 0f, Mathf.Max(0f, len - 0.01f));
                // NB: .time doit être fixé APRES avoir assigné le clip
                _ramp.time = startTime;
            }
            else
            {
                // Étirement simple via pitch pour remplir le temps restant
                // pitch < 1 => lecture plus lente (plus grave). On accepte la variation.
                float ratio = len / remaining; // ex: len=2s, remaining=4s => pitch=0.5
                _ramp.pitch = Mathf.Clamp(ratio, 0.1f, 3f);
                _ramp.time = 0f;
            }

            _ramp.Play();
        }
    }
}