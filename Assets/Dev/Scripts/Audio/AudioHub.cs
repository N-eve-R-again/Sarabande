// FILE: Assets/Dev/Scripts/Audio/AudioHub.cs
using UnityEngine;
using UnityEngine.Audio;
using System.Collections.Generic;

namespace Sarabande.Audio
{
    public class AudioHub : MonoBehaviour
    {
        public static AudioHub I { get; private set; }

        [Header("Mixer & Groups")]
        [SerializeField] private AudioMixer mixer;
        [SerializeField] private AudioMixerGroup masterGroup;
        [SerializeField] private AudioMixerGroup musicGroup;
        [SerializeField] private AudioMixerGroup sfxGroup;
        [SerializeField] private AudioMixerGroup uiGroup;
        [SerializeField] private AudioMixerGroup voiceGroup;
        [SerializeField] private AudioMixerGroup ambienceGroup;

        [Header("Exposed Param Names (dB)")]
        [SerializeField] private string masterParam = "MasterVol";
        [SerializeField] private string musicParam = "MusicVol";
        [SerializeField] private string sfxParam = "SFXVol";
        [SerializeField] private string uiParam = "UIVol";
        [SerializeField] private string voiceParam = "VoiceVol";
        [SerializeField] private string ambParam = "AmbVol";

        [Header("SFX Policy")]
        [SerializeField, Min(1)] private int sfxMaxVoices = 24;      // limite douce d'instances SFX concurrentes
        [SerializeField, Range(0, 255)] private int sfxDefaultPriority = 128; // 0 = plus haute priorité, 255 = plus basse

        [Header("Music Source")]
        [SerializeField] private AudioSource musicSource; // auto-créé si null
        [SerializeField] private bool dontDestroyOnLoad = false;

        private readonly List<AudioSource> _activeSfx = new();

        private void Awake()
        {
            if (I && I != this) { Destroy(gameObject); return; }
            I = this;
            if (dontDestroyOnLoad) DontDestroyOnLoad(gameObject);
            EnsureMusicSource();

            // Volumes par défaut (0 dB)
            SetLinear(masterParam, 1f);
            SetLinear(musicParam, 1f);
            SetLinear(sfxParam, 1f);
            if (!string.IsNullOrEmpty(uiParam)) SetLinear(uiParam, 1f);
            if (!string.IsNullOrEmpty(voiceParam)) SetLinear(voiceParam, 1f);
            if (!string.IsNullOrEmpty(ambParam)) SetLinear(ambParam, 1f);
        }

        private void EnsureMusicSource()
        {
            if (!musicSource)
            {
                var go = new GameObject("MusicSource");
                go.transform.SetParent(transform, false);
                musicSource = go.AddComponent<AudioSource>();
                musicSource.playOnAwake = false;
                musicSource.loop = true;
                musicSource.spatialBlend = 0f; // 2D
                musicSource.priority = 0;      // **hyper priorité** ? évite d’être virtualisée
                if (musicGroup) musicSource.outputAudioMixerGroup = musicGroup;
            }
        }

        // --- Volume API (0..1 linéaire ? dB exposé) ---
        static float ToDb(float x) => (x <= 0.0001f) ? -80f : Mathf.Log10(Mathf.Clamp(x, 0.0001f, 1f)) * 20f;
        void SetLinear(string param, float x) { if (mixer && !string.IsNullOrEmpty(param)) mixer.SetFloat(param, ToDb(x)); }

        public void SetMasterVolume(float v) => SetLinear(masterParam, v);
        public void SetMusicVolume(float v) => SetLinear(musicParam, v);
        public void SetSfxVolume(float v) => SetLinear(sfxParam, v);
        public void SetUIVolume(float v) { if (!string.IsNullOrEmpty(uiParam)) SetLinear(uiParam, v); }
        public void SetVoiceVolume(float v) { if (!string.IsNullOrEmpty(voiceParam)) SetLinear(voiceParam, v); }
        public void SetAmbienceVolume(float v) { if (!string.IsNullOrEmpty(ambParam)) SetLinear(ambParam, v); }

        // --- Music control ---
        public void PlayMusic(AudioClip clip, float vol = 1f, float fadeSeconds = 0.25f, bool loop = true)
        {
            if (!clip) return;
            EnsureMusicSource();
            musicSource.loop = loop;

            if (fadeSeconds > 0f && musicSource.isPlaying)
            {
                StopAllCoroutines();
                StartCoroutine(FadeMusicTo(clip, vol, fadeSeconds));
            }
            else
            {
                musicSource.clip = clip;
                musicSource.volume = vol;
                musicSource.Play();
            }
        }

        public void StopMusic(float fadeSeconds = 0.25f)
        {
            if (!musicSource) return;
            if (fadeSeconds > 0f && musicSource.isPlaying)
            {
                StopAllCoroutines();
                StartCoroutine(FadeOutMusic(fadeSeconds));
            }
            else musicSource.Stop();
        }

        private System.Collections.IEnumerator FadeMusicTo(AudioClip next, float vol, float dur)
        {
            float t = 0f, v0 = musicSource.volume;
            while (t < 1f) { t += Time.unscaledDeltaTime / dur; musicSource.volume = Mathf.Lerp(v0, 0f, t); yield return null; }
            musicSource.clip = next; musicSource.volume = 0f; musicSource.Play();
            t = 0f;
            while (t < 1f) { t += Time.unscaledDeltaTime / dur; musicSource.volume = Mathf.Lerp(0f, vol, t); yield return null; }
            musicSource.volume = vol;
        }

        private System.Collections.IEnumerator FadeOutMusic(float dur)
        {
            float t = 0f, v0 = musicSource.volume;
            while (t < 1f) { t += Time.unscaledDeltaTime / dur; musicSource.volume = Mathf.Lerp(v0, 0f, t); yield return null; }
            musicSource.Stop(); musicSource.volume = v0;
        }

        // --- SFX one-shots (route vers SFX bus) ---
        public AudioSource PlaySFXAt(AudioClip clip, Vector3 pos, float volume = 1f, float spatialBlend = 1f,
                                     float minDistance = 1f, float maxDistance = 20f, int? priority = null,
                                     AudioMixerGroup overrideGroup = null)
        {
            if (!clip) return null;

            CleanupFinishedSfx();
            if (_activeSfx.Count >= sfxMaxVoices)
                return null; // “cap” SFX : évite la surdose qui virtualise la musique

            var go = new GameObject("SFX_OneShot");
            go.transform.position = pos;
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = false;
            src.clip = clip;
            src.volume = Mathf.Clamp01(volume);
            src.spatialBlend = Mathf.Clamp01(spatialBlend);
            src.minDistance = minDistance;
            src.maxDistance = maxDistance;
            src.priority = priority ?? sfxDefaultPriority;
            src.outputAudioMixerGroup = overrideGroup ? overrideGroup : sfxGroup;

            src.Play();
            _activeSfx.Add(src);
            Destroy(go, clip.length + 0.2f);
            return src;
        }

        private void CleanupFinishedSfx()
        {
            for (int i = _activeSfx.Count - 1; i >= 0; --i)
            {
                var s = _activeSfx[i];
                if (!s || !s.isPlaying) _activeSfx.RemoveAt(i);
            }
        }

        // Dans AudioHub.cs (même classe)
        public AudioSource PlayVoiceAt(AudioClip clip, Vector3 pos, float volume = 1f)
        {
            // Voix en 2D par défaut (spatialBlend=0)
            return PlaySFXAt(clip, pos, volume, 0f, 1f, 20f, 100, voiceGroup);
        }

        public void RouteSourceToSFX(AudioSource src)
        {
            if (src) src.outputAudioMixerGroup = sfxGroup;
        }

        // Route une AudioSource vers le bon groupe du mixer
        public void RouteToSFX(AudioSource src) { if (src && sfxGroup) src.outputAudioMixerGroup = sfxGroup; }
        public void RouteToMusic(AudioSource src) { if (src && musicGroup) src.outputAudioMixerGroup = musicGroup; }
        public void RouteToVoice(AudioSource src) { if (src && voiceGroup) src.outputAudioMixerGroup = voiceGroup; }

    }
}

