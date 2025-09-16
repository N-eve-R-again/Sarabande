using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

namespace Sarabande.Audio
{
    /// <summary>
    /// Joue deux boucles (base/disco) parfaitement calées.
    /// - baseLoop : toujours audible hors disco
    /// - discoLoop : remplace base pendant une séquence disco
    /// </summary>
    public class MusicSystem : MonoBehaviour
    {
        [Header("Clips")]
        [SerializeField] private AudioClip baseLoop;
        [SerializeField] private AudioClip discoLoop;

        [Header("Volumes")]
        [SerializeField, Range(0f, 1f)] private float baseVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float discoVolume = 1f;

        [Header("Mix (optionnel)")]
        [SerializeField] private AudioMixerGroup output;

        [Header("Crossfade")]
        [Tooltip("En millisecondes. 0 = switch instantané.")]
        [SerializeField, Min(0f)] private float crossfadeMs = 0f;

        private AudioSource _baseSrc;
        private AudioSource _discoSrc;
        private bool _started;
        private Coroutine _fadeCo;

        private void Awake()
        {
            _baseSrc = CreateSrc("Music_Base");
            _discoSrc = CreateSrc("Music_Disco");
        }

        private void Start()
        {
            // Lance les deux boucles calées en DSP pour éviter le drift.
            StartLoops();
        }

        private AudioSource CreateSrc(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = true;
            if (output) src.outputAudioMixerGroup = output;
            return src;
        }

        private void StartLoops()
        {
            if (!baseLoop || !discoLoop) { Debug.LogWarning("[MusicSystem] Clips manquants."); return; }

            _baseSrc.clip = baseLoop;
            _discoSrc.clip = discoLoop;

            // Démarrage parfaitement calé
            double t0 = AudioSettings.dspTime + 0.05;
            _baseSrc.volume = baseVolume;
            _discoSrc.volume = 0f;

            _baseSrc.PlayScheduled(t0);
            _discoSrc.PlayScheduled(t0);

            _started = true;
        }

        public void EnterDisco()
        {
            if (!_started) return;
            Crossfade(toDisco: true);
        }

        public void ExitDisco()
        {
            if (!_started) return;
            Crossfade(toDisco: false);
        }

        private void Crossfade(bool toDisco)
        {
            if (_fadeCo != null) StopCoroutine(_fadeCo);
            _fadeCo = StartCoroutine(FadeRoutine(toDisco));
        }

        private IEnumerator FadeRoutine(bool toDisco)
        {
            float dur = crossfadeMs / 1000f;
            float bStart = _baseSrc.volume;
            float dStart = _discoSrc.volume;
            float bTarget = toDisco ? 0f : baseVolume;
            float dTarget = toDisco ? discoVolume : 0f;

            if (dur <= 0f)
            {
                _baseSrc.volume = bTarget;
                _discoSrc.volume = dTarget;
                yield break;
            }

            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime; // indépendant d’un éventuel timescale
                float k = Mathf.Clamp01(t / dur);
                _baseSrc.volume = Mathf.Lerp(bStart, bTarget, k);
                _discoSrc.volume = Mathf.Lerp(dStart, dTarget, k);
                yield return null;
            }
            _baseSrc.volume = bTarget;
            _discoSrc.volume = dTarget;
        }
    }
}