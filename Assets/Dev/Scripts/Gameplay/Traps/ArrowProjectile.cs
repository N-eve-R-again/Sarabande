// FILE: Assets/Dev/Scripts/Traps/ArrowProjectile.cs
//
// Rôle (résumé)
// - Projectile “flèche” instancié par ArrowTrapSystem.
// - Se déplace en ligne droite à vitesse constante, teste collisions : murs (raycast), Héros, NME.
// - À l’impact acteur : SFX + FX puis Reset (Héros via ResetManager, NME via ResetToInitial).
// - S’auto-détruit sur collision murale ou sortie de la grille.
//
// Invariants
// - AUCUN renommage de champs sérialisés ou signatures publiques.
// - Logique strictement identique. Modifs = commentaires + renommages **locaux** pour lisibilité.

using UnityEngine;
using Sarabande.Levels;
using Sarabande.Player;
using Sarabande.NME;

namespace Sarabande.Traps
{
    public class ArrowProjectile : MonoBehaviour
    {
        // ?????????????????????????????????????????????????????????????????????????????
        // Paramètres d'initialisation (injectés par ArrowTrapSystem)
        // ?????????????????????????????????????????????????????????????????????????????

        private Vector3 _dir;                         // direction monde normalisée
        private float _speed;                         // unités monde / seconde
        private float _cellSize;                      // taille d'une case (pour tolérances)
        private LayerMask _obstaclesMask;             // pour le raycast contre les murs/obstacles
        private LevelData _levelData;                 // bornes de la grille (sortie = destruction)
        private HeroController _hero;                 // cible possible
        private NMEController[] _nmes;                // cibles possibles
        private Sarabande.Core.ResetManager _resetManager;

        [SerializeField, Min(0f)] private float yRay = 0.05f;                  // hauteur du raycast obstacle
        [SerializeField, Range(0.1f, 0.49f)] private float hitHalf = 0.35f;    // demi-largeur de “bande” de hit
        [SerializeField, Min(0f)] private float movingSlack = 0.05f;           // marge supplémentaire (vit. + deltaTime)

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

        // ?????????????????????????????????????????????????????????????????????????????
        // Initialisation (appelée par le spawner)
        // ?????????????????????????????????????????????????????????????????????????????

        /// <summary>
        /// Initialise les paramètres physiques et les dépendances du projectile.
        /// </summary>
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

        /// <summary>Configure l’audio joué à l’impact (sur acteur).</summary>
        public void InitAudio(AudioClip hitClip, float volume, float spatialBlend, float minDist, float maxDist)
        {
            _hitLoveClip = hitClip;
            _hitVolume = Mathf.Clamp01(volume);
            _spatialBlend = Mathf.Clamp01(spatialBlend);
            _minDistance = Mathf.Max(0f, minDist);
            _maxDistance = Mathf.Max(_minDistance + 0.01f, maxDist);
        }

        /// <summary>Configure les FX joués à l’impact (sur acteur).</summary>
        public void InitFx(GameObject hitFxPrefab, Transform fxParent, float lifetime, float yOffset)
        {
            _hitFxPrefab = hitFxPrefab;
            _fxParent = fxParent;
            _fxLifetime = Mathf.Max(0.1f, lifetime);
            _impactYOffset = yOffset;
        }

        // ?????????????????????????????????????????????????????????????????????????????
        // Update
        // ?????????????????????????????????????????????????????????????????????????????

        /// <summary>
        /// Avance le projectile, teste les collisions (mur/Héros/NME), déclenche effets puis se détruit si nécessaire.
        /// </summary>
        private void Update()
        {
            float stepDistance = _speed * Time.deltaTime;
            if (stepDistance <= 0f) return;

            Vector3 currentPos = transform.position;
            Vector3 stepDelta = _dir * stepDistance;
            Vector3 nextPos = currentPos + stepDelta;

            // 1) Obstacles (raycast en Y + yRay)
            Vector3 rayOrigin = currentPos + Vector3.up * yRay;
            if (Physics.Raycast(rayOrigin, _dir, out RaycastHit hitInfo, stepDistance + 0.001f, _obstaclesMask))
            {
                transform.position = hitInfo.point;
                // Note: on ne joue pas d'FX/SFX sur les murs (seulement sur acteurs).
                Destroy(gameObject);
                return;
            }

            // 2) Hero hit ?
            if (_hero != null && HitsActor(currentPos, nextPos, _hero.WorldPos))
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
                    var nme = _nmes[i];
                    if (nme == null) continue;

                    if (HitsActor(currentPos, nextPos, nme.transform.position))
                    {
                        PlayHitLoveAt(nme.transform.position);
                        SpawnHitFx(nme.transform.position);
                        nme.ResetToInitial();
                        Destroy(gameObject);
                        return;
                    }
                }
            }

            // 4) Déplacement
            transform.position = nextPos;

            // 5) Hors bornes (détruit si en dehors de la grille)
            if (_levelData != null)
            {
                float maxX = _levelData.width * _cellSize;
                float maxZ = _levelData.height * _cellSize;
                if (nextPos.x < 0f || nextPos.x > maxX || nextPos.z < 0f || nextPos.z > maxZ)
                    Destroy(gameObject);
            }
        }

        // ?????????????????????????????????????????????????????????????????????????????
        // Helpers collisions / géométrie
        // ?????????????????????????????????????????????????????????????????????????????

        /// <summary>
        /// Teste si le segment [fromPos ? toPos] “traverse” la zone d’un acteur à la frame (bande orthogonale).
        /// La bande est perpendiculaire au mouvement : si la flèche va Nord/Sud, on teste en X, sinon en Z.
        /// </summary>
        private bool HitsActor(Vector3 fromPos, Vector3 toPos, Vector3 actorWorldPos)
        {
            float halfBand = hitHalf * _cellSize;
            halfBand += movingSlack + _speed * Time.deltaTime; // marge pour mouvement + discretisation

            if (Mathf.Abs(_dir.z) > 0.5f) // N/S -> bande en X
            {
                if (Mathf.Abs(actorWorldPos.x - fromPos.x) > halfBand) return false;
                return Between(actorWorldPos.z, fromPos.z, toPos.z);
            }
            else // E/W -> bande en Z
            {
                if (Mathf.Abs(actorWorldPos.z - fromPos.z) > halfBand) return false;
                return Between(actorWorldPos.x, fromPos.x, toPos.x);
            }
        }

        /// <summary>Retourne vrai si v est entre a et b (petit epsilon inclusif).</summary>
        private static bool Between(float value, float a, float b)
        {
            float min = Mathf.Min(a, b) - 0.0005f;
            float max = Mathf.Max(a, b) + 0.0005f;
            return value >= min && value <= max;
        }

        // ?????????????????????????????????????????????????????????????????????????????
        // Audio / FX helpers
        // ?????????????????????????????????????????????????????????????????????????????

        /// <summary>Joue l’effet sonore “love” à la position indiquée.</summary>
        private void PlayHitLoveAt(Vector3 worldPos)
        {
            if (!_hitLoveClip) return;

            Sarabande.Audio.AudioHub.I?.PlaySFXAt(
                _hitLoveClip,
                worldPos + Vector3.up * _impactYOffset, // petit offset pour éviter le sol
                _hitVolume,
                _spatialBlend,
                _minDistance,
                _maxDistance
            );
        }

        /// <summary>Instancie le FX d’impact (si présent) et programme sa destruction.</summary>
        private void SpawnHitFx(Vector3 worldPos)
        {
            if (_hitFxPrefab == null) return;

            var fxInstance = Object.Instantiate(
                _hitFxPrefab,
                worldPos + Vector3.up * _impactYOffset,
                Quaternion.identity,
                _fxParent
            );
            Object.Destroy(fxInstance, _fxLifetime);
        }
    }
}
