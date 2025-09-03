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

        // --- runtime ---
        private List<Vector2Int> _cells;   // cellules de la séquence active
        private List<float> _durations;    // secondes par étape (alignée à _cells)
        private int _step = -1;            // index de l’étape en cours (0..N-1)
        private float _deadline = 0f;      // Time.time auquel on “clôture” cette étape
        private bool _running = false;

        private void Awake()
        {
            if (!visuals) visuals = GetComponent<DiscoTilesVisuals>();
            if (!levelData || !hero || !visuals)
            {
                Debug.LogError("[DiscoSequenceSystem] Références manquantes (LevelData / Hero / Visuals).");
                enabled = false; return;
            }

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

            // 2) tick de fin d’étape
            if (Time.time >= _deadline)
            {
                // doit être sur la tuile ON courante au moment du tick
                if (hero.GridPos == _cells[_step])
                {
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
        }

        public void StopSequence()
        {
            _running = false;
        }

        // --- progression ---
        private void AdvanceStep()
        {
            // Étape _step vient d’être validée (Hero était dessus à l’instant du tick).
            _step++;

            if (_step >= _cells.Count)
            {
                // tout validé
                _running = false;
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

            // Les tuiles < _step restent ON (on ne les touche pas).
            // Les tuiles > _step+1 restent OFF (ou le deviendront quand on redémarre).

            _deadline = Time.time + _durations[_step];
        }

        private void FailSequence()
        {
            _running = false;
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
            if (visuals) visuals.SetAllOff();
        }
    }
}
