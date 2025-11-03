// FILE: Assets/Dev/Scripts/Audio/AudioHub.cs
//
// Rôle (résumé)
// - Point d’accès unique (singleton) pour l’audio : volumes du Mixer, musique, SFX one-shots, et routage des AudioSources.
// - Maintient une petite politique SFX (cap d’instances) pour éviter la virtualisation de la musique.
// - Fournit des helpers pour router vers les groupes (SFX/Music/Voice/UI/Ambience) du Mixer.
//
// Invariants
// - AUCUN renommage de champs sérialisés ni de méthodes publiques.
// - Logique strictement identique ; uniquement documentation, ordre plus lisible, et renommages locaux.

using UnityEngine;
using UnityEngine.Audio;
using System.Collections.Generic;

namespace Sarabande.Audio
{
    /// <summary>
    /// Hub audio global (singleton). Gère:
    /// - Volumes du <see cref="AudioMixer"/> (en dB via paramètres exposés).
    /// - Une source musique persistante (optionnel).
    /// - Le déclenchement de SFX one-shot avec limite souple de “voix”.
    /// - Des utilitaires de routage d’<see cref="AudioSource"/> vers les groupes du Mixer.
    /// </summary>
    public class AudioHub : MonoBehaviour
    {
        /// <summary>Instance globale du hub.</summary>
        public static AudioHub I { get; private set; }

        // ?????????????????????????????????????????????????????????????????????????????
        // Mixer & Groups (assignations via Inspector)
        // ?????????????????????????????????????????????????????????????????????????????

        [Header("Mixer & Groups")]
        [SerializeField] private AudioMixer mixer;
        [SerializeField] private AudioMixerGroup masterGroup;
        [SerializeField] private AudioMixerGroup musicGroup;
        [SerializeField] private AudioMixerGroup sfxGroup;
        [SerializeField] private AudioMixerGroup uiGroup;
        [SerializeField] private AudioMixerGroup voiceGroup;
        [SerializeField] private AudioMixerGroup ambienceGroup;

        // Paramètres exposés (en dB) dans le Mixer
        [Header("Exposed Param Names (dB)")]
        [SerializeField] private string masterParam = "MasterVol";
        [SerializeField] private string musicParam = "MusicVol";
        [SerializeField] private string sfxParam = "SFXVol";
        [SerializeField] private string uiParam = "UIVol";
        [SerializeField] private string voiceParam = "VoiceVol";
        [SerializeField] private string ambParam = "AmbVol";

        // Politique SFX (limites)
        [Header("SFX Policy")]
        [SerializeField, Min(1)] private int sfxMaxVoices = 24;                  // limite douce d'instances SFX
        [SerializeField, Range(0, 255)] private int sfxDefaultPriority = 128;    // 0 = + haute priorité

        // Musique persistante (optionnel)
        [Header("Music Source")]
        [SerializeField] private AudioSource musicSource; // auto-créé si null
        [SerializeField] private bool dontDestroyOnLoad = false;

        // ?????????????????????????????????????????????????????????????????????????????
        // Runtime
        // ?????????????????????????????????????????????????????????????????????????????

        private readonly List<AudioSource> _activeSfx = new();

        // ?????????????????????????????????????????????????????????????????????????????
        // Unity lifecycle
        // ?????????????????????????????????????????????????????????????????????????????

        private void Awake()
        {
            // Singleton
            if (I && I != this) { Destroy(gameObject); return; }
            I = this;

            if (dontDestroyOnLoad) DontDestroyOnLoad(gameObject);

            EnsureMusicSource();

            // Volumes par défaut (linéaire 1.0 -> 0 dB)
            SetLinear(masterParam, 1f);
            SetLinear(musicParam, 1f);
            SetLinear(sfxParam, 1f);
            if (!string.IsNullOrEmpty(uiParam)) SetLinear(uiParam, 1f);
            if (!string.IsNullOrEmpty(voiceParam)) SetLinear(voiceParam, 1f);
            if (!string.IsNullOrEmpty(ambParam)) SetLinear(ambParam, 1f);
        }

        // ?????????????????????????????????????????????????????????????????????????????
        // Setup / Sources
        // ?????????????????????????????????????????????????????????????????????????????

        /// <summary>
        /// Crée la source musique si absente, route vers le groupe “Music”, et la prépare au loop 2D.
        /// </summary>
        private void EnsureMusicSource()
        {
            if (musicSource) return;

            var musicGo = new GameObject("MusicSource");
            musicGo.transform.SetParent(transform, false);

            var created = musicGo.AddComponent<AudioSource>();
            created.playOnAwake = false;
            created.loop = true;
            created.spatialBlend = 0f;  // 2D
            created.priority = 0;   // haute priorité (évite la virtualisation)

            if (musicGroup) created.outputAudioMixerGroup = musicGroup;

            musicSource = created;
        }

        // ?????????????????????????????????????????????????????????????????????????????
        // Volume API (0..1 lin ? dB dans le mixer)
        // ?????????????????????????????????????????????????????????????????????????????

        /// <summary>Convertit un volume linéaire 0..1 en dB (paramètre exposé du Mixer).</summary>
        private static float ToDb(float linear)
            => (linear <= 0.0001f) ? -80f : Mathf.Log10(Mathf.Clamp(linear, 0.0001f, 1f)) * 20f;

        /// <summary>Assigne un paramètre exposé du Mixer à partir d’un volume linéaire 0..1.</summary>
        private void SetLinear(string exposedParam, float linear)
        {
            if (mixer && !string.IsNullOrEmpty(exposedParam))
                mixer.SetFloat(exposedParam, ToDb(linear));
        }

        /// <summary>Volume Master (linéaire 0..1).</summary>
        public void SetMasterVolume(float v) => SetLinear(masterParam, v);

        /// <summary>Volume Music (linéaire 0..1).</summary>
        public void SetMusicVolume(float v) => SetLinear(musicParam, v);

        /// <summary>Volume SFX (linéaire 0..1).</summary>
        public void SetSfxVolume(float v) => SetLinear(sfxParam, v);

        /// <summary>Volume UI (linéaire 0..1) — ignoré si paramètre non renseigné.</summary>
        public void SetUIVolume(float v) { if (!string.IsNullOrEmpty(uiParam)) SetLinear(uiParam, v); }

        /// <summary>Volume Voice (linéaire 0..1) — ignoré si paramètre non renseigné.</summary>
        public void SetVoiceVolume(float v) { if (!string.IsNullOrEmpty(voiceParam)) SetLinear(voiceParam, v); }

        /// <summary>Volume Ambience (linéaire 0..1) — ignoré si paramètre non renseigné.</summary>
        public void SetAmbienceVolume(float v) { if (!string.IsNullOrEmpty(ambParam)) SetLinear(ambParam, v); }

        // ?????????????????????????????????????????????????????????????????????????????
        // Music control (une source unique)
        // ?????????????????????????????????????????????????????????????????????????????

        /// <summary>
        /// Joue un clip musique (loop optionnel), avec fondu crossfade si un morceau est déjà en cours.
        /// </summary>
        /// <param name="clip">Clip à jouer.</param>
        /// <param name="vol">Volume cible (linéaire 0..1).</param>
        /// <param name="fadeSeconds">Durée de crossfade. ?0 = switch instantané.</param>
        /// <param name="loop">Active le loop.</param>
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

        /// <summary>Arrête la musique, avec fade-out optionnel.</summary>
        public void StopMusic(float fadeSeconds = 0.25f)
        {
            if (!musicSource) return;

            if (fadeSeconds > 0f && musicSource.isPlaying)
            {
                StopAllCoroutines();
                StartCoroutine(FadeOutMusic(fadeSeconds));
            }
            else
            {
                musicSource.Stop();
            }
        }

        /// <summary>Transitionne vers un nouveau clip musique avec crossfade (out puis in).</summary>
        private System.Collections.IEnumerator FadeMusicTo(AudioClip next, float vol, float dur)
        {
            float t = 0f;
            float v0 = musicSource.volume;

            // Fade-out
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / Mathf.Max(0.0001f, dur);
                musicSource.volume = Mathf.Lerp(v0, 0f, t);
                yield return null;
            }

            // Switch + fade-in
            musicSource.clip = next;
            musicSource.volume = 0f;
            musicSource.Play();

            t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / Mathf.Max(0.0001f, dur);
                musicSource.volume = Mathf.Lerp(0f, vol, t);
                yield return null;
            }

            musicSource.volume = vol;
        }

        /// <summary>Fade-out puis stop de la musique courante.</summary>
        private System.Collections.IEnumerator FadeOutMusic(float dur)
        {
            float t = 0f;
            float v0 = musicSource.volume;

            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / Mathf.Max(0.0001f, dur);
                musicSource.volume = Mathf.Lerp(v0, 0f, t);
                yield return null;
            }

            musicSource.Stop();
            musicSource.volume = v0; // on remet le niveau initial pour un prochain Play
        }

        // ?????????????????????????????????????????????????????????????????????????????
        // SFX one-shots
        // ?????????????????????????????????????????????????????????????????????????????

        /// <summary>
        /// Joue un SFX one-shot positionné, routé par défaut vers le groupe SFX.
        /// Limite le nombre de sources simultanées à <see cref="sfxMaxVoices"/>.
        /// </summary>
        /// <returns>La <see cref="AudioSource"/> créée (peut être <c>null</c> si cap atteint).</returns>
        public AudioSource PlaySFXAt(
            AudioClip clip,
            Vector3 pos,
            float volume = 1f,
            float spatialBlend = 1f,
            float minDistance = 1f,
            float maxDistance = 20f,
            int? priority = null,
            AudioMixerGroup overrideGroup = null)
        {
            if (!clip) return null;

            CleanupFinishedSfx();

            if (_activeSfx.Count >= sfxMaxVoices)
                return null; // cap atteint : on laisse tomber ce SFX

            var sfxGo = new GameObject("SFX_OneShot");
            sfxGo.transform.position = pos;

            var sfxSource = sfxGo.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.loop = false;
            sfxSource.clip = clip;
            sfxSource.volume = Mathf.Clamp01(volume);
            sfxSource.spatialBlend = Mathf.Clamp01(spatialBlend);
            sfxSource.minDistance = minDistance;
            sfxSource.maxDistance = maxDistance;
            sfxSource.priority = priority ?? sfxDefaultPriority;
            sfxSource.outputAudioMixerGroup = overrideGroup ? overrideGroup : sfxGroup;

            sfxSource.Play();
            _activeSfx.Add(sfxSource);

            // Auto-destroy du GameObject (clip length + petite marge)
            Destroy(sfxGo, clip.length + 0.2f);

            return sfxSource;
        }

        /// <summary>Nettoie la liste des SFX terminés ou détruits.</summary>
        private void CleanupFinishedSfx()
        {
            for (int i = _activeSfx.Count - 1; i >= 0; --i)
            {
                var candidate = _activeSfx[i];
                if (!candidate || !candidate.isPlaying)
                    _activeSfx.RemoveAt(i);
            }
        }

        /// <summary>
        /// Joue une voix (2D par défaut) en la routant vers le bus “Voice”.
        /// Wrapper simple autour de <see cref="PlaySFXAt"/>.
        /// </summary>
        public AudioSource PlayVoiceAt(AudioClip clip, Vector3 pos, float volume = 1f)
        {
            // Voix en 2D (spatialBlend = 0)
            return PlaySFXAt(clip, pos, volume, 0f, 1f, 20f, 100, voiceGroup);
        }

        // ?????????????????????????????????????????????????????????????????????????????
        // Routing helpers
        // ?????????????????????????????????????????????????????????????????????????????

        /// <summary>Route explicitement une source vers le bus SFX.</summary>
        public void RouteSourceToSFX(AudioSource src)
        {
            if (src) src.outputAudioMixerGroup = sfxGroup;
        }

        /// <summary>Route vers SFX (alias historique, conservé pour compatibilité).</summary>
        public void RouteToSFX(AudioSource src) { if (src && sfxGroup) src.outputAudioMixerGroup = sfxGroup; }
        /// <summary>Route vers Music.</summary>
        public void RouteToMusic(AudioSource src) { if (src && musicGroup) src.outputAudioMixerGroup = musicGroup; }
        /// <summary>Route vers Voice.</summary>
        public void RouteToVoice(AudioSource src) { if (src && voiceGroup) src.outputAudioMixerGroup = voiceGroup; }
    }
}
