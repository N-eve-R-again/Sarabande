using UnityEngine;
using Sarabande.Levels;
using Sarabande.Player;
using Sarabande.NME;

namespace Sarabande.Traps
{
    public class ArrowProjectile : MonoBehaviour
    {
        // params spawn
        private Vector3 _dir;
        private float _speed;
        private float _cellSize;
        private LayerMask _obstaclesMask;
        private LevelData _levelData;
        private HeroController _hero;
        private NMEController[] _nmes;
        private Sarabande.Core.ResetManager _resetManager;

        [SerializeField, Min(0f)] private float yRay = 0.05f;
        [SerializeField, Range(0.1f, 0.49f)] private float hitHalf = 0.35f;
        [SerializeField, Min(0f)] private float movingSlack = 0.05f;

        // Audio impact
        private AudioClip _hitLoveClip;
        private float _hitVolume = 1f;
        private float _spatialBlend = 1f;
        private float _minDistance = 2f;
        private float _maxDistance = 18f;

        // FX impact
        private GameObject _hitFxPrefab;
        private Transform _fxParent;
        private float _fxLifetime = 1.5f;
        private float _impactYOffset = 0.05f;

        public void Init(
            Vector3 dir, float speed, float cellSize, LayerMask obstaclesMask,
            LevelData levelData, HeroController hero, NMEController[] nmes,
            Sarabande.Core.ResetManager resetManager)
        {
            _dir = dir.normalized;
            _speed = speed;
            _cellSize = cellSize;
            _obstaclesMask = obstaclesMask;
            _levelData = levelData;
            _hero = hero;
            _nmes = nmes;
            _resetManager = resetManager;
        }

        public void InitAudio(AudioClip hitClip, float volume, float spatialBlend, float minDist, float maxDist)
        {
            _hitLoveClip = hitClip;
            _hitVolume = Mathf.Clamp01(volume);
            _spatialBlend = Mathf.Clamp01(spatialBlend);
            _minDistance = Mathf.Max(0f, minDist);
            _maxDistance = Mathf.Max(_minDistance + 0.01f, maxDist);
        }

        public void InitFx(GameObject hitFxPrefab, Transform fxParent, float lifetime, float yOffset)
        {
            _hitFxPrefab = hitFxPrefab;
            _fxParent = fxParent;
            _fxLifetime = Mathf.Max(0.1f, lifetime);
            _impactYOffset = yOffset;
        }

        private void Update()
        {
            float step = _speed * Time.deltaTime;
            if (step <= 0f) return;

            Vector3 origin = transform.position;
            Vector3 move = _dir * step;
            Vector3 next = origin + move;

            // 1) Obstacles
            Vector3 rayOrigin = origin + Vector3.up * yRay;
            if (Physics.Raycast(rayOrigin, _dir, out RaycastHit hit, step + 0.001f, _obstaclesMask))
            {
                transform.position = hit.point;
                // (FX/son uniquement sur ACTEUR, pas sur mur — à activer si tu veux)
                // SpawnHitFx(hit.point);
                Destroy(gameObject);
                return;
            }

            // 2) Hero hit ?
            if (_hero != null && HitsActor(origin, next, _hero.WorldPos))
            {
                PlayHitLoveAt(_hero.WorldPos);
                SpawnHitFx(_hero.WorldPos);
                if (_resetManager != null) _resetManager.ResetWithRewind();
                Destroy(gameObject);
                return;
            }

            // 3) NME hit ?
            if (_nmes != null)
            {
                for (int i = 0; i < _nmes.Length; i++)
                {
                    var n = _nmes[i];
                    if (n == null) continue;
                    if (HitsActor(origin, next, n.transform.position))
                    {
                        PlayHitLoveAt(n.transform.position);
                        SpawnHitFx(n.transform.position);
                        n.ResetToInitial();
                        Destroy(gameObject);
                        return;
                    }
                }
            }

            // 4) Move
            transform.position = next;

            // 5) Hors bornes
            if (_levelData != null)
            {
                float maxX = _levelData.width * _cellSize;
                float maxZ = _levelData.height * _cellSize;
                if (next.x < 0f || next.x > maxX || next.z < 0f || next.z > maxZ)
                    Destroy(gameObject);
            }
        }

        private bool HitsActor(Vector3 a, Vector3 b, Vector3 actorPos)
        {
            float half = hitHalf * _cellSize;
            half += movingSlack + _speed * Time.deltaTime;

            if (Mathf.Abs(_dir.z) > 0.5f) // N/S -> bande en X
            {
                if (Mathf.Abs(actorPos.x - a.x) > half) return false;
                return Between(actorPos.z, a.z, b.z);
            }
            else // E/W -> bande en Z
            {
                if (Mathf.Abs(actorPos.z - a.z) > half) return false;
                return Between(actorPos.x, a.x, b.x);
            }
        }

        private static bool Between(float v, float a, float b)
        {
            float min = Mathf.Min(a, b) - 0.0005f;
            float max = Mathf.Max(a, b) + 0.0005f;
            return v >= min && v <= max;
        }

        // --- Audio / FX helpers ---
        private void PlayHitLoveAt(Vector3 pos)
        {
            if (!_hitLoveClip) return;
            var go = new GameObject("SFX_Arrow_HitLove");
            go.transform.position = pos + Vector3.up * _impactYOffset; // <-- offset appliqué
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = false;
            src.clip = _hitLoveClip;
            src.volume = _hitVolume;
            src.spatialBlend = _spatialBlend;
            src.minDistance = _minDistance;
            src.maxDistance = _maxDistance;
            src.Play();
            Object.Destroy(go, _hitLoveClip.length + 0.1f);
        }

        private void SpawnHitFx(Vector3 pos)
        {
            if (_hitFxPrefab == null) return;
            var fx = Object.Instantiate(
                _hitFxPrefab,
                pos + Vector3.up * _impactYOffset,           // <-- offset appliqué
                Quaternion.identity,
                _fxParent
            );
            Object.Destroy(fx, _fxLifetime);
        }
    }
}
