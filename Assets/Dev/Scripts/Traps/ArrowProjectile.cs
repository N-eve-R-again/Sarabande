using UnityEngine;
using Sarabande.Levels;
using Sarabande.Player;
using Sarabande.NME;

namespace Sarabande.Traps
{
    /// <summary>
    /// Projectile flèche, avance en ligne droite (cardinale).
    /// - S'arrête au 1er obstacle (raycast sur Obstacles).
    /// - Touche HÉRO => ResetWithRewind (niveau).
    /// - Touche NME => ResetToInitial() de l’NME seulement.
    /// - Auto-détruit hors des bornes de la grille.
    /// </summary>
    public class ArrowProjectile : MonoBehaviour
    {
        // params fournis au spawn
        private Vector3 _dir;                  // normalized
        private float _speed;
        private float _cellSize;
        private LayerMask _obstaclesMask;
        private LevelData _levelData;
        private HeroController _hero;
        private NMEController[] _nmes;
        private Sarabande.Core.ResetManager _resetManager;

        // petits réglages
        [SerializeField, Min(0f)] private float yRay = 0.05f;           // hauteur du ray
        [SerializeField, Range(0.1f, 0.49f)] private float hitHalf = 0.35f; // “largeur” du couloir de hit (en fraction de case)

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

        private void Update()
        {
            float step = _speed * Time.deltaTime;
            if (step <= 0f) return;

            Vector3 origin = transform.position;
            Vector3 move = _dir * step;
            Vector3 next = origin + move;

            // 1) Obstacles (murs + thin walls) via raycast
            Vector3 rayOrigin = origin + Vector3.up * yRay;
            if (Physics.Raycast(rayOrigin, _dir, out RaycastHit hit, step + 0.001f, _obstaclesMask))
            {
                // place la flèche à l’impact pour le feeling
                transform.position = hit.point;
                Destroy(gameObject);
                return;
            }

            // 2) Hero hit ? (test continu sur le segment [origin,next] avec “couloir” autour de l’axe)
            if (_hero != null && HitsActor(origin, next, _hero.WorldPos))
            {
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
                        n.ResetToInitial();     // seul l’NME est reset
                        Destroy(gameObject);
                        return;
                    }
                }
            }

            // 4) Move
            transform.position = next;

            // 5) Hors bornes -> destroy
            if (_levelData != null)
            {
                float maxX = _levelData.width * _cellSize;
                float maxZ = _levelData.height * _cellSize;
                if (next.x < 0f || next.x > maxX || next.z < 0f || next.z > maxZ)
                {
                    Destroy(gameObject);
                }
            }
        }

        private bool HitsActor(Vector3 a, Vector3 b, Vector3 actorPos)
        {
            // Mouvement cardinale uniquement ? on simplifie
            // NORTH/SOUTH : on regarde la bande en X ; EAST/WEST : la bande en Z
            float half = hitHalf * _cellSize;

            if (Mathf.Abs(_dir.z) > 0.5f) // déplacement sur Z (N/S)
            {
                bool alignedX = Mathf.Abs(actorPos.x - a.x) <= half;
                if (!alignedX) return false;

                // acteur Z entre a.z et b.z (ordre quelconque)
                return Between(actorPos.z, a.z, b.z);
            }
            else // déplacement sur X (E/W)
            {
                bool alignedZ = Mathf.Abs(actorPos.z - a.z) <= half;
                if (!alignedZ) return false;

                return Between(actorPos.x, a.x, b.x);
            }
        }

        private static bool Between(float v, float a, float b)
        {
            // v dans [min(a,b) .. max(a,b)] (petit epsilon)
            float min = Mathf.Min(a, b) - 0.0005f;
            float max = Mathf.Max(a, b) + 0.0005f;
            return v >= min && v <= max;
        }
    }
}
