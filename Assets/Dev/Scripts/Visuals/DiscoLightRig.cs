// FILE: Assets/Dev/Scripts/VFX/DiscoLightRig.cs
using System.Collections.Generic;
using UnityEngine;
using Sarabande.Core;   // LevelContext (optionnel)
using Sarabande.Disco; // DiscoSequenceSystem
using UnityEngine.Rendering;

namespace Sarabande.VFX
{
    /// <summary>
    /// Crée/Anime des spots colorés pendant une séquence disco.
    /// - S'active sur onSequenceStart, se coupe sur Success/Fail
    /// - Orbite douce + wobble, regarde le centre du plateau
    /// - Zéro dépendance forte : LevelContext facultatif (sert juste pour centrer/échelle)
    /// </summary>
    public class DiscoLightRig : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private DiscoSequenceSystem disco;       // assigne ta DiscoSequenceSystem ici
        [SerializeField] private LevelContext levelContext;       // facultatif (sert pour width/height)
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
        [SerializeField] private float height = 4.5f;        // Y des spots
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
        [SerializeField] private Vector2 yawSweepSpeedRange = new(0.5f, 1.2f);   // Hz approx (rad/s divisés par 2?)
        [SerializeField] private bool desyncYawPhases = true;

        [Header("Aiming")]
        [SerializeField, Range(0.2f, 1.2f)] private float sweepRadiusMul = 0.9f; // rayon de balayage sur le sol
        [SerializeField, Range(0.1f, 20f)] private float aimSmoothing = 8f;      // lissage du look (Slerp)

        [Header("Environment dimming")]
        [SerializeField] private bool dimEnvironment = true;           // active le fade d’ambiance
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

        private bool _envCaptured = false;
        private AmbientMode _ambientMode0;
        private float _ambientIntensity0;
        private Color _ambientLight0;
        private readonly List<Light> _dir0 = new();
        private readonly List<float> _dirInt0 = new();
        private Coroutine _envCo;

        private void OnEnable()
        {
            if (disco)
            {
                disco.onSequenceStart.AddListener(OnDiscoStart);
                disco.onSequenceSuccess.AddListener(OnDiscoStop);
                disco.onSequenceFail.AddListener(OnDiscoStop);
            }
            // Si on veut que ça existe hors disco, on peut construire au démarrage
            if (!onlyWhenDisco) EnsureBuilt();
        }

        private void OnDisable()
        {
            if (disco)
            {
                disco.onSequenceStart.RemoveListener(OnDiscoStart);
                disco.onSequenceSuccess.RemoveListener(OnDiscoStop);
                disco.onSequenceFail.RemoveListener(OnDiscoStop);
            }
            if (destroyOnStop) DestroyRig();
            else SetRigEnabled(false);
        }

        private void Update()
        {
            if (!_active || _root == null) return;

            float t = Time.time;

            // Étendue pour positionner et pour cibler au sol
            float rx = _half.x * radiusMul;
            float rz = _half.y * radiusMul;
            float rSweep = Mathf.Min(_half.x, _half.y) * sweepRadiusMul;

            for (int i = 0; i < _lights.Count; i++)
            {
                var L = _lights[i];
                if (!L) continue;

                // --- TRAJECTOIRE (position du projecteur) ---
                float ang = (t * _speed[i] * Mathf.PI * 2f) + _phase[i];

                Vector3 pos;
                if (useLissajous)
                {
                    // Lissajous = plus riche que l’ellipse simple
                    float x = Mathf.Sin(ang * lissaFreqX);
                    float z = Mathf.Sin((ang + 1.234f) * lissaFreqZ); // léger décalage de phase Z
                    pos = _center + new Vector3(x * rx, height, z * rz);
                }
                else
                {
                    // ellipse/orbite
                    pos = _center + new Vector3(Mathf.Cos(ang) * rx, height, Mathf.Sin(ang) * rz);
                }
                L.transform.position = pos;

                // --- BALAYAGE (orientation du faisceau) ---
                // 1) Yaw de base = direction vers le centre (en radians)
                Vector3 toCenter = _center - pos;
                float baseYaw = Mathf.Atan2(toCenter.z, toCenter.x); // [-?..?]

                // 2) Modulation sinusoïdale (pan) pour balayer la piste
                float yaw = baseYaw + Mathf.Sin(t * _yawSpeed[i] + _yawPhase[i]) * Mathf.Deg2Rad * yawSweepAmplitude;

                // 3) Point-cible sur le sol : cercle de rayon rSweep
                Vector3 target = _center + new Vector3(Mathf.Cos(yaw) * rSweep, 0f, Mathf.Sin(yaw) * rSweep);

                // 4) Look vers le point-cible, avec léger wobble (tangage) + lissage
                Vector3 fwd = (target - pos).normalized;
                var look = Quaternion.LookRotation(fwd, Vector3.up);

                if (wobbleDeg > 0f)
                {
                    float wob = Mathf.Sin((t + i * 0.37f) * wobbleSpeed) * wobbleDeg;
                    look *= Quaternion.Euler(wob, 0f, 0f);
                }

                L.transform.rotation = Quaternion.Slerp(L.transform.rotation, look, 1f - Mathf.Exp(-aimSmoothing * Time.deltaTime));
            }
        }

        // --- Disco events ---
        private void OnDiscoStart()
        {
            EnsureBuilt();
            SetRigEnabled(true);
            StartFadeEnvironment(true);   // << assombrit la scène
        }

        private void OnDiscoStop()
        {
            StartFadeEnvironment(false);  // << rétablit la lumière
            if (destroyOnStop) DestroyRig();
            else SetRigEnabled(false);
        }

        // --- Build/Enable/Destroy ---
        private void EnsureBuilt()
        {
            if (_root != null) return;

            // Calcule le centre/extent depuis LevelContext si dispo, sinon fallback 8x8
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

            for (int i = 0; i < lightCount; i++)
            {
                var go = new GameObject($"DiscoSpot_{i}");
                go.transform.SetParent(_root, false);

                var l = go.AddComponent<Light>();
                l.type = LightType.Spot;
                l.spotAngle = spotAngle;
                l.innerSpotAngle = Mathf.Min(innerSpotAngle, spotAngle - 1f);
                l.intensity = intensity; // <-- use serialized field
                l.range = range;         // <-- use serialized field
                l.shadows = LightShadows.Soft;

                if (useCookies)
                    l.cookie = GenerateRadialCookie(cookieResolution, cookieHardness);

                // --- COLOR: pick from palette (fallback = random bright) ---
                if (palette != null && palette.Length > 0)
                    l.color = palette[i % palette.Length];
                else
                    l.color = Color.HSVToRGB(Random.value, 1f, 1f);

                _lights.Add(l);
                // Vitesse/phase de la trajectoire (position)
                _speed.Add(Random.Range(speedRange.x, speedRange.y));
                _phase.Add(randomizePhases ? Random.value * Mathf.PI * 2f : (i * Mathf.PI * 0.5f));

                // Vitesse/phase du balayage (orientation indépendante)
                _yawSpeed.Add(Random.Range(yawSweepSpeedRange.x, yawSweepSpeedRange.y));
                _yawPhase.Add(desyncYawPhases ? Random.value * Mathf.PI * 2f : 0f);
            }

            // place initialement
            _active = true;
            Update();
            _active = false;

            SetRigEnabled(!onlyWhenDisco); // si on veut visible hors disco
        }

        // Génération d’un cookie circulaire avec bord adouci
        private Texture2D GenerateRadialCookie(int res, float hardness)
        {
            var tex = new Texture2D(res, res, TextureFormat.R8, false, true);
            tex.wrapMode = TextureWrapMode.Clamp;
            float cx = (res - 1) * 0.5f;
            float cy = (res - 1) * 0.5f;
            float rMax = Mathf.Min(cx, cy);

            // hardness ~ 0.0..1.0 : 0 = très doux, 1 = très dur
            float edge0 = rMax * 0.92f;                  // rayon “plein”
            float edge1 = Mathf.Lerp(rMax, edge0, hardness); // zone de dégradé

            var pixels = new Color32[res * res];
            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    float dx = x - cx, dy = y - cy;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);

                    float a = 0f;
                    if (r <= edge1) a = 1f;                             // plein
                    else if (r < rMax) a = Mathf.InverseLerp(rMax, edge1, r); // dégradé
                    else a = 0f;

                    byte b = (byte)Mathf.RoundToInt(a * 255f);
                    pixels[y * res + x] = new Color32(b, b, b, 255);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return tex;
        }

        private void SetRigEnabled(bool on)
        {
            _active = on;
            if (_root) _root.gameObject.SetActive(on);
        }

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
        }

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

        private System.Collections.IEnumerator FadeEnvironment(bool dim)
        {
            if (!dimEnvironment) yield break;
            CaptureEnvironmentOnce();

            // Ambient (deux cas : Skybox/Trilight -> ambientIntensity ; Flat -> ambientLight color)
            float ambStartInt = RenderSettings.ambientIntensity;
            float ambTargetInt = _ambientIntensity0 * (dim ? ambientMultiplier : 1f);

            Color ambStartCol = RenderSettings.ambientLight;
            Color ambTargetCol = _ambientLight0 * (dim ? ambientMultiplier : 1f);

            // Directional(s)
            var dirStart = new float[_dir0.Count];
            var dirTarget = new float[_dir0.Count];
            for (int i = 0; i < _dir0.Count; i++)
            {
                dirStart[i] = _dir0[i] ? _dir0[i].intensity : 0f;
                dirTarget[i] = _dirInt0[i] * (dim ? directionalMultiplier : 1f);
            }

            float t = 0f, dur = Mathf.Max(0.0001f, envFadeSeconds);
            while (t < 1f)
            {
                t += Time.deltaTime / dur;
                float k = Mathf.Clamp01(t);

                if (_ambientMode0 == AmbientMode.Skybox || _ambientMode0 == AmbientMode.Trilight)
                    RenderSettings.ambientIntensity = Mathf.Lerp(ambStartInt, ambTargetInt, k);
                else
                    RenderSettings.ambientLight = Color.Lerp(ambStartCol, ambTargetCol, k);

                for (int i = 0; i < _dir0.Count; i++)
                    if (_dir0[i]) _dir0[i].intensity = Mathf.Lerp(dirStart[i], dirTarget[i], k);

                yield return null;
            }

            // snap final
            if (_ambientMode0 == AmbientMode.Skybox || _ambientMode0 == AmbientMode.Trilight)
                RenderSettings.ambientIntensity = ambTargetInt;
            else
                RenderSettings.ambientLight = ambTargetCol;

            for (int i = 0; i < _dir0.Count; i++)
                if (_dir0[i]) _dir0[i].intensity = dirTarget[i];
        }

        private void StartFadeEnvironment(bool dim)
        {
            if (!dimEnvironment) return;
            if (_envCo != null) StopCoroutine(_envCo);
            _envCo = StartCoroutine(FadeEnvironment(dim));
        }
        public void StartRig() { OnDiscoStart(); }
        public void StopRig() { OnDiscoStop(); }
    }
}
