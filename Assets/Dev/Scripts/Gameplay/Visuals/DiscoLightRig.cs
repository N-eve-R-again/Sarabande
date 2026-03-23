// FILE: Assets/Dev/Scripts/VFX/DiscoLightRig.cs
//
// Résumé
// -------
// Génère et anime un rig de spots colorés pendant une séquence Disco.
// - S'active sur onSequenceStart et s'arrête sur Success/Fail (via DiscoSequenceSystem).
// - Les spots orbitent au-dessus du plateau (ellipse ou Lissajous), avec un léger wobble (tangage).
// - Le faisceau balaie le sol autour du centre (pan "yaw"), phases désynchronisées pour de la variété.
// - Optionnel : atténue l'ambiance de la scène pendant la Disco (ambient + lights directionnelles).
// - Aucune dépendance forte : LevelContext est facultatif (sert uniquement à dimensionner/centrer).
//
// Notes implémentation
// --------------------
// - Construction paresseuse : EnsureBuilt() crée le rig la première fois (ou au OnEnable si nécessaire).
// - Aucun changement d’API publique. Tous les renommages ne concernent que des variables locales.
// - Les méthodes StartRig/StopRig publiques sont conservées (helpers simples appelant OnDiscoStart/OnDiscoStop).

using System.Collections.Generic;
using UnityEngine;
using Sarabande.Core;   // LevelContext (optionnel)
using UnityEngine.Rendering;

namespace Sarabande.VFX
{
    /// <summary>
    /// Crée et anime un rig de spots colorés synchronisés à une séquence Disco.
    /// </summary>
    public class DiscoLightRig : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private LevelContext levelContext;       // Facultatif (pour width/height)
        [SerializeField, Min(0.001f)] private float cellSize = 1f;

        [Header("Rig")]
        [SerializeField, Range(1, 12)] private int lightCount = 4;
        [SerializeField]
        private Color[] palette =
        {
            new Color(1.00f, 0.30f, 0.40f),
            new Color(1.00f, 0.65f, 0.10f),
            new Color(0.10f, 0.75f, 1.00f),
            new Color(0.45f, 0.35f, 1.00f),
        };
        [SerializeField] private float height = 4.5f;        // Altitude Y des spots
        [SerializeField, Range(0.4f, 1.2f)] private float radiusMul = 0.85f; // Ø orbite = boardMax * radiusMul
        [SerializeField] private Vector2 speedRange = new(0.15f, 0.45f);     // vitesse d'orbite (rev/sec)
        [SerializeField, Range(10f, 90f)] private float spotAngle = 42f;
        [SerializeField, Range(0f, 79f)] private float innerSpotAngle = 30f;
        [SerializeField, Range(0.1f, 30f)] private float range = 18f;
        [SerializeField, Range(0f, 8f)] private float intensity = 3.2f;

        [Header("Motion polish")]
        [SerializeField, Range(0f, 20f)] private float wobbleDeg = 8f;   // petit tangage
        [SerializeField, Range(0.1f, 6f)] private float wobbleSpeed = 1.6f;
        [SerializeField] private bool randomizePhases = true;

        [Header("Lifecycle")]
        [SerializeField] private bool onlyWhenDisco = true;  // crée/active le rig uniquement pendant la disco
        [SerializeField] private bool destroyOnStop = true;  // sinon, on garde mais on désactive

        [SerializeField] private bool useCookies = true;
        [SerializeField, Range(64, 1024)] private int cookieResolution = 256;
        [SerializeField, Range(0f, 1f)] private float cookieHardness = 0.7f; // 0=soft, 1=dur

        [Header("Motion mode")]
        [SerializeField] private bool useLissajous = true;          // sinon: ellipse/orbite
        [SerializeField, Range(0.3f, 1.2f)] private float lissaFreqX = 0.75f;
        [SerializeField, Range(0.3f, 1.2f)] private float lissaFreqZ = 1.15f;

        [Header("Beam sweep (pan)")]
        [SerializeField, Range(0f, 120f)] private float yawSweepAmplitude = 65f; // ° d’écart max
        [SerializeField] private Vector2 yawSweepSpeedRange = new(0.5f, 1.2f);   // Hz approx
        [SerializeField] private bool desyncYawPhases = true;

        [Header("Aiming")]
        [SerializeField, Range(0.2f, 1.2f)] private float sweepRadiusMul = 0.9f; // rayon de balayage au sol
        [SerializeField, Range(0.1f, 20f)] private float aimSmoothing = 8f;      // lissage du look (Slerp)

        [Header("Environment dimming")]
        [SerializeField] private bool dimEnvironment = true;           // fade d’ambiance pendant la Disco
        [SerializeField, Range(0f, 1f)] private float ambientMultiplier = 0.35f; // % de l’ambient initial
        [SerializeField, Range(0f, 1f)] private float directionalMultiplier = 0.5f; // % intensité Sun(s)
        [SerializeField, Min(0f)] private float envFadeSeconds = 0.35f;
        [SerializeField] private bool includeDirectionalLights = true; // touche aussi les Directional

        // --- runtime ---
        private Transform _root;
        private readonly List<Light> _lights = new();
        private readonly List<float> _speed = new();
        private readonly List<float> _phase = new();
        private readonly List<float> _yawSpeed = new();
        private readonly List<float> _yawPhase = new();
        private Vector3 _center;
        private Vector2 _half; // demi-étendue monde (x: demi-largeur, y: demi-hauteur)
        private bool _active;

        // État de l’environnement (capture + fade)
        private bool _envCaptured = false;
        private AmbientMode _ambientMode0;
        private float _ambientIntensity0;
        private Color _ambientLight0;
        private readonly List<Light> _dir0 = new();
        private readonly List<float> _dirInt0 = new();
        private Coroutine _envCo;

        // ====== Unity callbacks & wiring ======

        /// <summary>
        /// Abonne le rig aux évènements de Disco et construit éventuellement le rig hors-Disco.
        /// </summary>
        private void OnEnable()
        {
            // Si visible hors Disco, on construit dès maintenant.
            if (!onlyWhenDisco) EnsureBuilt();
        }

        /// <summary>
        /// Désabonne les évènements et détruit/masque le rig selon la config.
        /// </summary>
        private void OnDisable()
        {

            if (destroyOnStop) DestroyRig();
            else SetRigEnabled(false);
        }

        /// <summary>
        /// Anime en continu positions et orientations des spots quand le rig est actif.
        /// </summary>
        private void Update()
        {
            if (!_active || _root == null) return;

            float timeNow = Time.time;

            // Étendue utile pour trajectoires et cibles au sol
            float orbitRadiusX = _half.x * radiusMul;
            float orbitRadiusZ = _half.y * radiusMul;
            float sweepRadiusGround = Mathf.Min(_half.x, _half.y) * sweepRadiusMul;

            for (int i = 0; i < _lights.Count; i++)
            {
                var spotLight = _lights[i];
                if (!spotLight) continue;

                // --- TRAJECTOIRE (position du projecteur) ---
                float angle = (timeNow * _speed[i] * Mathf.PI * 2f) + _phase[i];

                Vector3 worldPos;
                if (useLissajous)
                {
                    // Trajectoire de Lissajous : plus riche qu'une ellipse simple.
                    float x = Mathf.Sin(angle * lissaFreqX);
                    float z = Mathf.Sin((angle + 1.234f) * lissaFreqZ); // petit déphasage
                    worldPos = _center + new Vector3(x * orbitRadiusX, height, z * orbitRadiusZ);
                }
                else
                {
                    // Orbite elliptique classique.
                    worldPos = _center + new Vector3(Mathf.Cos(angle) * orbitRadiusX, height, Mathf.Sin(angle) * orbitRadiusZ);
                }
                spotLight.transform.position = worldPos;

                // --- BALAYAGE (orientation du faisceau) ---
                // 1) Yaw de base = direction du centre (radians)
                Vector3 toCenter = _center - worldPos;
                float baseYawRad = Mathf.Atan2(toCenter.z, toCenter.x);

                // 2) Modulation sinusoïdale (pan)
                float yawRad = baseYawRad + Mathf.Sin(timeNow * _yawSpeed[i] + _yawPhase[i]) * Mathf.Deg2Rad * yawSweepAmplitude;

                // 3) Point-cible au sol sur un cercle de rayon sweepRadiusGround
                Vector3 targetOnGround = _center + new Vector3(Mathf.Cos(yawRad) * sweepRadiusGround, 0f, Mathf.Sin(yawRad) * sweepRadiusGround);

                // 4) Look vers la cible, avec wobble (tangage) + lissage
                Vector3 forwardDir = (targetOnGround - worldPos).normalized;
                Quaternion lookRotation = Quaternion.LookRotation(forwardDir, Vector3.up);

                if (wobbleDeg > 0f)
                {
                    float wobbleDegrees = Mathf.Sin((timeNow + i * 0.37f) * wobbleSpeed) * wobbleDeg;
                    lookRotation *= Quaternion.Euler(wobbleDegrees, 0f, 0f);
                }

                spotLight.transform.rotation = Quaternion.Slerp(
                    spotLight.transform.rotation,
                    lookRotation,
                    1f - Mathf.Exp(-aimSmoothing * Time.deltaTime)
                );
            }
        }

        // ====== Réaction aux évènements Disco ======

        /// <summary>
        /// Démarre le rig et lance l’assombrissement d’ambiance quand la Disco commence.
        /// </summary>
        private void OnDiscoStart()
        {
            EnsureBuilt();
            SetRigEnabled(true);
            StartFadeEnvironment(dim: true);
        }

        /// <summary>
        /// Rétablit l’ambiance et détruit/masque le rig quand la Disco s’arrête.
        /// </summary>
        private void OnDiscoStop()
        {
            StartFadeEnvironment(dim: false);
            if (destroyOnStop) DestroyRig();
            else SetRigEnabled(false);
        }

        // ====== Construction / activation / destruction ======

        /// <summary>
        /// Construit le rig (parent + lumières) si nécessaire, positionné et dimensionné selon le plateau.
        /// </summary>
        private void EnsureBuilt()
        {
            if (_root != null) return;

            // Centre/extent depuis LevelContext si dispo, sinon fallback 8x8.
            int w = 8, h = 8;
            if (levelContext && levelContext.LevelData)
            {
                w = Mathf.Max(1, levelContext.LevelData.width);
                h = Mathf.Max(1, levelContext.LevelData.height);
            }
            _center = new Vector3(w * cellSize * 0.5f, 0f, h * cellSize * 0.5f);
            _half = new Vector2(w * cellSize * 0.5f, h * cellSize * 0.5f);

            _root = new GameObject("DiscoLightRig_Runtime").transform;
            _root.SetParent(transform, false);

            _lights.Clear();
            _speed.Clear();
            _phase.Clear();
            _yawSpeed.Clear();
            _yawPhase.Clear();

            for (int i = 0; i < lightCount; i++)
            {
                var lightGO = new GameObject($"DiscoSpot_{i}");
                lightGO.transform.SetParent(_root, false);

                var lightComponent = lightGO.AddComponent<Light>();
                lightComponent.type = LightType.Spot;
                lightComponent.spotAngle = spotAngle;
                lightComponent.innerSpotAngle = Mathf.Min(innerSpotAngle, spotAngle - 1f);
                lightComponent.intensity = intensity;
                lightComponent.range = range;
                lightComponent.shadows = LightShadows.Soft;

                if (useCookies)
                    lightComponent.cookie = GenerateRadialCookie(cookieResolution, cookieHardness);

                // Couleur depuis la palette (fallback : couleur vive aléatoire)
                if (palette != null && palette.Length > 0)
                    lightComponent.color = palette[i % palette.Length];
                else
                    lightComponent.color = Color.HSVToRGB(Random.value, 1f, 1f);

                _lights.Add(lightComponent);

                // Trajectoire (position)
                _speed.Add(Random.Range(speedRange.x, speedRange.y));
                _phase.Add(randomizePhases ? Random.value * Mathf.PI * 2f : (i * Mathf.PI * 0.5f));

                // Balayage (orientation)
                _yawSpeed.Add(Random.Range(yawSweepSpeedRange.x, yawSweepSpeedRange.y));
                _yawPhase.Add(desyncYawPhases ? Random.value * Mathf.PI * 2f : 0f);
            }

            // Positionnement initial propre (un tick d'Update forcé)
            _active = true;
            Update();
            _active = false;

            // Visible d'emblée si le rig doit exister hors Disco
            SetRigEnabled(!onlyWhenDisco);
        }

        /// <summary>
        /// Génère un cookie radial (gris 8 bits) avec bord adouci.
        /// </summary>
        private Texture2D GenerateRadialCookie(int resolution, float hardness01)
        {
            var tex = new Texture2D(resolution, resolution, TextureFormat.R8, false, true);
            tex.wrapMode = TextureWrapMode.Clamp;

            float cx = (resolution - 1) * 0.5f;
            float cy = (resolution - 1) * 0.5f;
            float rMax = Mathf.Min(cx, cy);

            // hardness : 0 = très doux, 1 = dur
            float edgeInner = rMax * 0.92f;                       // rayon “plein”
            float edgeBlend = Mathf.Lerp(rMax, edgeInner, hardness01); // zone de dégradé

            var pixels = new Color32[resolution * resolution];
            for (int py = 0; py < resolution; py++)
            {
                for (int px = 0; px < resolution; px++)
                {
                    float dx = px - cx, dy = py - cy;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);

                    float alpha;
                    if (r <= edgeBlend) alpha = 1f;                              // plein
                    else if (r < rMax) alpha = Mathf.InverseLerp(rMax, edgeBlend, r); // dégradé
                    else alpha = 0f;

                    byte v = (byte)Mathf.RoundToInt(alpha * 255f);
                    pixels[py * resolution + px] = new Color32(v, v, v, 255);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return tex;
        }

        /// <summary>
        /// Active/désactive l’objet runtime du rig et son animation.
        /// </summary>
        private void SetRigEnabled(bool enable)
        {
            _active = enable;
            if (_root) _root.gameObject.SetActive(enable);
        }

        /// <summary>
        /// Détruit complètement le rig runtime (GameObject parent + listes).
        /// </summary>
        private void DestroyRig()
        {
            _active = false;
            if (_root)
            {
                if (Application.isPlaying) Destroy(_root.gameObject);
                else DestroyImmediate(_root.gameObject);
            }
            _root = null;
            _lights.Clear();
            _speed.Clear();
            _phase.Clear();
            _yawSpeed.Clear();
            _yawPhase.Clear();
        }

        // ====== Environment dimming ======

        /// <summary>
        /// Capture une fois l’état d’éclairage global (ambient + directionnelles) pour le restaurer ensuite.
        /// </summary>
        private void CaptureEnvironmentOnce()
        {
            if (_envCaptured) return;

            _ambientMode0 = RenderSettings.ambientMode;
            _ambientIntensity0 = RenderSettings.ambientIntensity;
            _ambientLight0 = RenderSettings.ambientLight;

            _dir0.Clear();
            _dirInt0.Clear();
            if (includeDirectionalLights)
            {
                var all = FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var l in all)
                {
                    if (l && l.type == LightType.Directional)
                    {
                        _dir0.Add(l);
                        _dirInt0.Add(l.intensity);
                    }
                }
            }
            _envCaptured = true;
        }

        /// <summary>
        /// Coroutine de fondu d’ambiance (vers sombre ou clair).
        /// </summary>
        private System.Collections.IEnumerator FadeEnvironment(bool dim)
        {
            if (!dimEnvironment) yield break;
            CaptureEnvironmentOnce();

            // Ambient : intensity (Skybox/Trilight) ou color (Flat)
            float startAmbientIntensity = RenderSettings.ambientIntensity;
            float targetAmbientIntensity = _ambientIntensity0 * (dim ? ambientMultiplier : 1f);

            Color startAmbientColor = RenderSettings.ambientLight;
            Color targetAmbientColor = _ambientLight0 * (dim ? ambientMultiplier : 1f);

            // Directionnelles
            var startDirIntensities = new float[_dir0.Count];
            var targetDirIntensities = new float[_dir0.Count];
            for (int i = 0; i < _dir0.Count; i++)
            {
                startDirIntensities[i] = _dir0[i] ? _dir0[i].intensity : 0f;
                targetDirIntensities[i] = _dirInt0[i] * (dim ? directionalMultiplier : 1f);
            }

            float t = 0f;
            float duration = Mathf.Max(0.0001f, envFadeSeconds);
            while (t < 1f)
            {
                t += Time.deltaTime / duration;
                float k = Mathf.Clamp01(t);

                if (_ambientMode0 == AmbientMode.Skybox || _ambientMode0 == AmbientMode.Trilight)
                    RenderSettings.ambientIntensity = Mathf.Lerp(startAmbientIntensity, targetAmbientIntensity, k);
                else
                    RenderSettings.ambientLight = Color.Lerp(startAmbientColor, targetAmbientColor, k);

                for (int i = 0; i < _dir0.Count; i++)
                    if (_dir0[i]) _dir0[i].intensity = Mathf.Lerp(startDirIntensities[i], targetDirIntensities[i], k);

                yield return null;
            }

            // Snap final
            if (_ambientMode0 == AmbientMode.Skybox || _ambientMode0 == AmbientMode.Trilight)
                RenderSettings.ambientIntensity = targetAmbientIntensity;
            else
                RenderSettings.ambientLight = targetAmbientColor;

            for (int i = 0; i < _dir0.Count; i++)
                if (_dir0[i]) _dir0[i].intensity = targetDirIntensities[i];
        }

        /// <summary>
        /// Lance/relance la coroutine de fondu d’ambiance.
        /// </summary>
        private void StartFadeEnvironment(bool dim)
        {
            if (!dimEnvironment) return;
            if (_envCo != null) StopCoroutine(_envCo);
            _envCo = StartCoroutine(FadeEnvironment(dim));
        }

        // ====== Helpers publics conservés ======

        /// <summary>Alias pratique pour démarrer le rig (équivaut à l’évènement de début Disco).</summary>
        public void StartRig() { OnDiscoStart(); }

        /// <summary>Alias pratique pour arrêter le rig (équivaut à l’évènement de fin Disco).</summary>
        public void StopRig() { OnDiscoStop(); }
    }
}
