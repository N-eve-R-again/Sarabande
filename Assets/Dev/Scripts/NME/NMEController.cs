using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Sarabande.Core;
using Sarabande.Levels;
using Sarabande.Player; // HeroController

namespace Sarabande.NME
{
    public enum NMEState { HeroUnspotted, HeroSpotted }

    /// <summary>
    /// NME qui :
    /// - attend en HeroUnspotted, surveille avec un FOV (90-180°, portée illimitée, bloqué par 'Obstacles')
    /// - passe en HeroSpotted s'il voit HÉRO (ou entend un bruit), puis le poursuit (BFS) en respectant murs + murs fins
    /// - rotation instantanée vers la direction du prochain step
    /// - attaque télégrafiée quand adjacent (pré-delay -> telegraph -> impact)
    /// </summary>
    public class NMEController : MonoBehaviour, IResettable
    {
        [Header("Data & Refs")]
        [SerializeField] private LevelData levelData;
        [SerializeField, Min(0.001f)] private float cellSize = 1f;
        [SerializeField] private HeroController hero;

        [Header("Movement (Spotted)")]
        [SerializeField, Min(0.05f)] private float stepDuration = 0.45f;
        [SerializeField, Min(0f)] private float interStepPause = 0.10f;
        [SerializeField, Min(0.05f)] private float repathInterval = 0.20f;

        [Header("Detection (Unspotted)")]
        [SerializeField, Range(90f, 180f)] private float fovDegrees = 120f;
        [SerializeField, Min(0f)] private float losHeight = 0.5f;           // hauteur du ray (Y)
        [SerializeField] private LayerMask obstaclesMask;                    // coche "Obstacles" dans l'Inspector
        [SerializeField] private EdgeDirection initialFacing = EdgeDirection.East;

        [Header("Attack")]
        [SerializeField, Min(0.01f)] private float attackPreDelay = 0.12f;  // délai avant de lever le bras
        [SerializeField, Min(0.05f)] private float attackWindup = 0.35f;    // fenêtre d’esquive (télégrafie)
        [SerializeField, Min(0.05f)] private float attackRecover = 0.20f;   // petit temps après l’impact
        [SerializeField] private Material telegraphMaterial;
        [SerializeField, Min(0.05f)] private float telegraphDiameter = 0.9f;
        [SerializeField, Min(0f)] private float telegraphY = 0.02f;

        [Header("Debug")]
        [SerializeField] private bool drawFovGizmos = true;
        [SerializeField, Min(1f)] private float gizmoFovRadius = 20f;

        [Header("Hero Conflict")]
        [SerializeField, Range(0f, 0.5f)] private float heroYieldThreshold = 0.15f;     // t max pour que l’NME cède
        [SerializeField, Range(0f, 0.5f)] private float heroVacateBlockThreshold = 0.25f; // case du héros reste bloquée tant qu’il ne l’a pas assez libérée

        [SerializeField] private ResetManager resetManager;  // à assigner (LevelRoot)

        private NMEState _state = NMEState.HeroUnspotted;

        // positions/logique
        private Vector2Int _gridPos;
        private bool _isMoving = false;
        private bool _isAttacking = false;
        private float _readyAt = 0f;
        private float _nextRepathAt = 0f;

        private Vector2Int _lastGoal; // dernière case Héros connue pour laquelle on a calculé un chemin (évite d'attendre le recalcul du pathfinding pour bouger)

        public bool IsStepping => _isMoving;
        public Vector2Int FromCell { get; private set; }
        public Vector2Int ToCell { get; private set; }
        public float MoveProgress { get; private set; } // 0..1 pendant un step

        // collisions (murs / murs fins)
        private HashSet<Vector2Int> _blockedCells;
        private HashSet<(Vector2Int a, Vector2Int b)> _thinBlockers;
        private HashSet<(Vector2Int a, Vector2Int b)> _dynamicEdgeBlocks
            = new HashSet<(Vector2Int, Vector2Int)>();
        private HashSet<Vector2Int> _dynamicBlockCells = new HashSet<Vector2Int>();

        // path courant (séquence de cases à suivre, exclut la case actuelle)
        private readonly List<Vector2Int> _path = new();

        private void Start()
        {
            if (levelData == null || hero == null)
            {
                Debug.LogError("[NME] LevelData ou Hero manquant.");
                enabled = false;
                return;
            }

            BuildCollisionSets();

            _gridPos = new Vector2Int(levelData.nmeSpawn.x, levelData.nmeSpawn.z);
            transform.position = GridCenter(_gridPos);

            // init exposées (si tu les as)
            FromCell = ToCell = _gridPos;
            MoveProgress = 0f;

            // cache de la dernière cible (position du héros)
            _lastGoal = hero.GridPos;

            SetFacing(initialFacing);
            _state = NMEState.HeroUnspotted;

            _nextRepathAt = Time.time;
        }


        private void Update()
        {
            if (resetManager != null && resetManager.IsResetInProgress) return;

            switch (_state)
            {
                case NMEState.HeroUnspotted:
                    {
                        // reste immobile, scrute le cône
                        if (CanSeeHero())
                        {
                            _state = NMEState.HeroSpotted;
                            _nextRepathAt = 0f; // repath immédiat
                        }
                        return;
                    }

                case NMEState.HeroSpotted:
                    {
                        if (_isMoving) return;
                        if (Time.time < _readyAt) return;

                        var heroPos = hero.GridPos;

                        // Adjacent -> attaque télégrafiée
                        if (IsDirectlyAdjacent(_gridPos, heroPos))
                        {
                            if (!_isAttacking)
                            {
                                Vector2Int dir = heroPos - _gridPos;   // forcément cardinal ici
                                StartCoroutine(AttackRoutine(heroPos, dir));
                            }
                            return; // pas de déplacement pendant l’attaque
                        }

                        // Repath périodique
                        // Repath "juste-à-temps" : si le chemin est vide OU si la cible a changé,
                        // on recalcule tout de suite (peu coûteux sur 8x8)
                        // Sinon, on ne recalculera que périodiquement (suivi fluide d’un chemin déjà trouvé)
                        bool goalChanged = heroPos != _lastGoal;
                        bool needRepath = (_path.Count == 0) || goalChanged;

                        if (needRepath || Time.time >= _nextRepathAt)
                        {
                            RecomputePath(heroPos);
                            _lastGoal = heroPos;
                            _nextRepathAt = Time.time + repathInterval;
                        }

                        // Avancer d'une case si on a un chemin
                        if (_path.Count > 0)
                        {
                            var next = _path[0];

                            // petite ceinture : s'assurer que 'next' est adjacent à la position actuelle
                            int md = Mathf.Abs(next.x - _gridPos.x) + Mathf.Abs(next.y - _gridPos.y);
                            if (md != 1)
                            {
                                // le chemin est obsolète (par ex. après un reset/annulation) : on recalcule
                                RecomputePath(heroPos);
                                if (_path.Count == 0)
                                {
                                    _readyAt = Time.time + interStepPause;
                                    return;
                                }
                                next = _path[0];
                            }

                            _path.RemoveAt(0);
                            FaceDirection(next - _gridPos);
                            StartCoroutine(StepTo(next));
                        }
                        else
                        {
                            // Aucun chemin possible (bouché) -> on attend proprement
                            _readyAt = Time.time + interStepPause;
                        }
                        return;
                    }
            }
        }

        // --- Détection ---

        private bool CanSeeHero()
        {
            // angle (FOV) + raycast bloqué par Obstacles
            Vector3 origin = transform.position + Vector3.up * losHeight;
            Vector3 target = hero.WorldPos + Vector3.up * losHeight;

            Vector3 toHero = target - origin;
            Vector3 toHeroXZ = new Vector3(toHero.x, 0f, toHero.z);
            if (toHeroXZ.sqrMagnitude < 0.0001f) return true; // même case presque

            // Test angle
            Vector3 fwdXZ = new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;
            float angle = Vector3.Angle(fwdXZ, toHeroXZ.normalized);
            if (angle > (fovDegrees * 0.5f)) return false;

            // Test LOS (murs + murs fins)
            float dist = toHero.magnitude;
            if (Physics.Raycast(origin, toHero / dist, dist, obstaclesMask))
                return false;

            return true;
        }

        public void HeardNoise()
        {
            if (_state == NMEState.HeroUnspotted)
                _state = NMEState.HeroSpotted;
        }

        private bool IsHeroInsideCell(Vector2Int cell)
        {
            Vector3 center = GridCenter(cell);
            float half = cellSize * 0.5f;
            Vector3 p = hero.WorldPos;

            float eps = 0.001f;
            bool insideX = p.x >= center.x - half + eps && p.x <= center.x + half - eps;
            bool insideZ = p.z >= center.z - half + eps && p.z <= center.z + half - eps;
            return insideX && insideZ;
        }

        // --- Steps & path ---

        private IEnumerator StepTo(Vector2Int target)
        {
            _isMoving = true;

            FromCell = _gridPos;
            ToCell = target;
            MoveProgress = 0f;

            Vector3 start = transform.position;
            Vector3 end = GridCenter(target);

            // Si conflit direct sur la même target au démarrage du step : l'NME cède si sa progression reste <= seuil
            bool checkYield = false;
            if (hero != null && hero.IsStepping && target == hero.ToCell)
            {
                checkYield = true;
            }

            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / stepDuration;
                MoveProgress = t;
                // Fenêtre de cession (priorité HÉRO) : uniquement en tout début de step
                if (checkYield && t <= heroYieldThreshold)
                {
                    // le héros vise la même case ; l'NME s'efface
                    transform.position = start;        // snap back immédiat
                    _isMoving = false;
                    _readyAt = Time.time + interStepPause;
                    _path.Clear();                     // évite un "prochain nœud" obsolète
                    _nextRepathAt = 0f;                // repath immédiat au prochain Update
                    yield break;
                }
                if (t > 1f) t = 1f;
                transform.position = Vector3.Lerp(start, end, t);
                yield return null;
            }

            _gridPos = target;
            _isMoving = false;
            _readyAt = Time.time + interStepPause;

            MoveProgress = 0f;
            FromCell = ToCell = _gridPos;
        }

        private void RecomputePath(Vector2Int goal)
        {
            _path.Clear();

            var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
            var q = new Queue<Vector2Int>();
            var visited = new HashSet<Vector2Int>();

            q.Enqueue(_gridPos);
            visited.Add(_gridPos);

            bool found = false;

            while (q.Count > 0)
            {
                var cur = q.Dequeue();

                if (cur == goal)
                {
                    found = true;
                    break;
                }

                foreach (var nb in NeighborsTowardGoal(cur, goal))
                {
                    if (visited.Contains(nb)) continue;
                    visited.Add(nb);
                    cameFrom[nb] = cur;
                    q.Enqueue(nb);
                }
            }

            if (!found) return;

            var cur2 = goal;
            var stack = new Stack<Vector2Int>();
            while (cur2 != _gridPos)
            {
                stack.Push(cur2);
                if (!cameFrom.TryGetValue(cur2, out cur2))
                {
                    _path.Clear();
                    return;
                }
            }
            while (stack.Count > 0)
                _path.Add(stack.Pop());
        }

        private IEnumerable<Vector2Int> NeighborsTowardGoal(Vector2Int c, Vector2Int goal)
        {
            var dirs = new[]
            {
        Vector2Int.right, // tie-breaker stable: Est d'abord
        Vector2Int.left,
        Vector2Int.up,
        Vector2Int.down
    };

            var cand = new List<(Vector2Int n, int dist, int tie)>(4);

            for (int i = 0; i < dirs.Length; i++)
            {
                var d = dirs[i];
                var n = c + d;
                if (!InsideBounds(n)) continue;
                if (_blockedCells.Contains(n)) continue;
                if (HasThinWallBetween(c, n)) continue;

                int dist = Mathf.Abs(n.x - goal.x) + Mathf.Abs(n.y - goal.y);
                cand.Add((n, dist, i));
            }

            cand.Sort((a, b) =>
            {
                int cmp = a.dist.CompareTo(b.dist);
                if (cmp != 0) return cmp;
                return a.tie.CompareTo(b.tie); // ordre stable Est>Ouest>Nord>Sud
            });

            foreach (var p in cand) yield return p.n;
        }

        // --- Collisions / util ---

        private void BuildCollisionSets()
        {
            _blockedCells = new HashSet<Vector2Int>();
            foreach (var c in levelData.nonWalkables)
                _blockedCells.Add(new Vector2Int(c.x, c.z));

            if (_dynamicBlockCells != null)
            {
                foreach (var c in _dynamicBlockCells)
                    _blockedCells.Add(c);
            }

            _thinBlockers = new HashSet<(Vector2Int, Vector2Int)>();
            foreach (var e in levelData.thinWalls)
            {
                var a = new Vector2Int(e.a.x, e.a.z);
                var b = new Vector2Int(e.b.x, e.b.z);
                _thinBlockers.Add(NormalizeEdge(a, b));
            }
        }

        private static (Vector2Int, Vector2Int) NormalizeEdge(Vector2Int a, Vector2Int b)
        {
            if (a.x < b.x) return (a, b);
            if (a.x > b.x) return (b, a);
            return (a.y <= b.y) ? (a, b) : (b, a);
        }

        private bool HasThinWallBetween(Vector2Int from, Vector2Int to)
        {
            var key = NormalizeEdge(from, to);
            return _thinBlockers.Contains(key) || _dynamicEdgeBlocks.Contains(key);
        }

        /// <summary>
        /// Deux cases sont "directement adjacentes" si elles sont cardinales ET
        /// qu'aucun mur fin ne les sépare.
        /// </summary>
        private bool IsDirectlyAdjacent(Vector2Int a, Vector2Int b)
        {
            var d = a - b;
            int md = Mathf.Abs(d.x) + Mathf.Abs(d.y);
            if (md != 1) return false;                 // pas cardinal / pas adjacent
            return !HasThinWallBetween(a, b);          // adjacent oui, mais pas à travers un thin wall
        }

        private bool InsideBounds(Vector2Int c)
        {
            return c.x >= 0 && c.x < levelData.width && c.y >= 0 && c.y < levelData.height;
        }

        private Vector3 GridCenter(Vector2Int c)
        {
            return new Vector3((c.x + 0.5f) * cellSize, 0f, (c.y + 0.5f) * cellSize);
        }

        // --- Facing / rotation ---

        private void SetFacing(EdgeDirection dir)
        {
            Vector3 fwd = DirToVector(dir);
            if (fwd.sqrMagnitude > 0f)
                transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);
        }

        private void FaceDirection(Vector2Int delta)
        {
            if (delta == Vector2Int.right) SetFacing(EdgeDirection.East);
            else if (delta == Vector2Int.left) SetFacing(EdgeDirection.West);
            else if (delta == Vector2Int.up) SetFacing(EdgeDirection.North);
            else if (delta == Vector2Int.down) SetFacing(EdgeDirection.South);
        }

        private static Vector3 DirToVector(EdgeDirection dir)
        {
            return dir switch
            {
                EdgeDirection.North => new Vector3(0f, 0f, 1f),
                EdgeDirection.East => new Vector3(1f, 0f, 0f),
                EdgeDirection.South => new Vector3(0f, 0f, -1f),
                EdgeDirection.West => new Vector3(-1f, 0f, 0f),
                _ => Vector3.forward
            };
        }

        // --- Reset ---

        public void ResetToInitial()
        {
            StopAllCoroutines();
            MoveProgress = 0f;
            FromCell = ToCell = _gridPos;
            _isAttacking = false;
            _path.Clear();
            _isMoving = false;
            _readyAt = 0f;
            _nextRepathAt = Time.time;

            BuildCollisionSets();

            _dynamicEdgeBlocks.Clear();

            _gridPos = new Vector2Int(levelData.nmeSpawn.x, levelData.nmeSpawn.z);
            transform.position = GridCenter(_gridPos);

            SetFacing(initialFacing);
            _state = NMEState.HeroUnspotted;
        }

        // --- Telégraphie / Attaque ---

        private GameObject CreateTelegraphMarker(Vector2Int cell)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "AttackTelegraph";
            var col = go.GetComponent<Collider>(); if (col) Destroy(col);

            var center = GridCenter(cell);
            float halfHeight = 0.01f;

            go.transform.position = new Vector3(center.x, telegraphY + halfHeight, center.z);
            go.transform.localScale = new Vector3(telegraphDiameter, 0.01f, telegraphDiameter);
            go.transform.SetParent(transform, true); // conserve la position MONDE

            var mr = go.GetComponent<MeshRenderer>();
            if (mr)
            {
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                if (telegraphMaterial) mr.sharedMaterial = telegraphMaterial;
            }

            return go;
        }

        private IEnumerator AttackRoutine(Vector2Int targetCell, Vector2Int dir)
        {
            _isAttacking = true;
            _path.Clear();          // on gèle la poursuite pendant l'attaque
            FaceDirection(dir);     // verrouille la direction dès le début

            // 1) Pré-délai (avant de lever le bras / télégraphe)
            float t = 0f;
            while (t < attackPreDelay)
            {
                t += Time.deltaTime;
                yield return null;
            }

            // Si HERO a quitté la case au terme du pré-délai -> on annule l'attaque
            if (!IsHeroInsideCell(targetCell))
            {
                _isAttacking = false;
                _readyAt = Time.time + interStepPause; // petite pause punitive
                yield break;
            }

            // 2) Télégrafie (disque rouge sur la case visée)
            var marker = CreateTelegraphMarker(targetCell);

            t = 0f;
            while (t < attackWindup)
            {
                t += Time.deltaTime;
                yield return null;
            }

            // 3) Impact
            if (IsHeroInsideCell(targetCell))
            {
                if (marker) Destroy(marker);
                if (resetManager != null) resetManager.ResetWithRewind();
                else Debug.LogWarning("[NME] ResetManager non assigné pour l’attaque.");
                yield break; // pas de recover ni reprise avant reset
            }

            if (marker) Destroy(marker);

            // 4) Recover
            t = 0f;
            while (t < attackRecover)
            {
                t += Time.deltaTime;
                yield return null;
            }

            _isAttacking = false;
            _readyAt = Time.time + interStepPause;
        }

        // exposer la position logique si besoin ailleurs
        public Vector2Int GridPos => _gridPos;

        // --- GIZMOS (FOV & LOS) ---

        private void OnDrawGizmosSelected()
        {
            if (!drawFovGizmos) return;

            // Couleurs
            Color colFov = new Color(1f, 0.92f, 0.016f, 0.6f); // jaune
            Color colLosOk = new Color(0.2f, 1f, 0.2f, 0.9f);    // vert
            Color colLosHit = new Color(1f, 0.2f, 0.2f, 0.9f);    // rouge

            // Rayon FOV (adapter à la grille si LevelData dispo)
            float radius = gizmoFovRadius;
            if (levelData != null)
                radius = Mathf.Max(levelData.width, levelData.height) * cellSize * 1.2f;

            // Plan XZ
            Vector3 pos = transform.position + Vector3.up * 0.01f;
            Vector3 fwd = new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;
            if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;

            // Dessine l’éventail (arc FOV)
            Gizmos.color = colFov;
            int steps = 32;
            float half = Mathf.Deg2Rad * (fovDegrees * 0.5f);
            for (int i = 0; i < steps; i++)
            {
                float t0 = Mathf.Lerp(-half, half, i / (float)steps);
                float t1 = Mathf.Lerp(-half, half, (i + 1) / (float)steps);

                Vector3 d0 = Quaternion.AngleAxis(Mathf.Rad2Deg * t0, Vector3.up) * fwd;
                Vector3 d1 = Quaternion.AngleAxis(Mathf.Rad2Deg * t1, Vector3.up) * fwd;

                Gizmos.DrawLine(pos + d0 * radius, pos + d1 * radius);
            }
            // Deux rayons de bord
            Vector3 left = Quaternion.AngleAxis(-fovDegrees * 0.5f, Vector3.up) * fwd;
            Vector3 right = Quaternion.AngleAxis(fovDegrees * 0.5f, Vector3.up) * fwd;
            Gizmos.DrawLine(pos, pos + left * radius);
            Gizmos.DrawLine(pos, pos + right * radius);

            // LOS vers le héros (si dispo)
            if (hero != null)
            {
                Vector3 origin = transform.position + Vector3.up * losHeight;
                Vector3 target = hero.WorldPos + Vector3.up * losHeight;
                Vector3 dir = (target - origin);
                float dist = dir.magnitude;
                if (dist > 0.0001f)
                {
                    dir /= dist;
                    if (Physics.Raycast(origin, dir, out RaycastHit hit, dist, obstaclesMask))
                    {
                        Gizmos.color = colLosHit;
                        Gizmos.DrawLine(origin, hit.point);
                        Gizmos.DrawSphere(hit.point, 0.05f);
                    }
                    else
                    {
                        Gizmos.color = colLosOk;
                        Gizmos.DrawLine(origin, target);
                        Gizmos.DrawSphere(target, 0.05f);
                    }
                }
            }
        }
        private void EnsureSets()
        {
            if (_blockedCells == null) BuildCollisionSets();
        }
        public void AddDynamicBlockCell(Vector2Int c)
        {
            EnsureSets();
            _dynamicBlockCells.Add(c);
            _blockedCells.Add(c);
        }

        public void RemoveDynamicBlockCell(Vector2Int c)
        {
            EnsureSets();
            _dynamicBlockCells.Remove(c);

            bool isStatic = false;
            foreach (var gc in levelData.nonWalkables)
                if (gc.x == c.x && gc.z == c.y) { isStatic = true; break; }

            if (!isStatic) _blockedCells.Remove(c);
        }
        public void AddDynamicEdgeBlock(Vector2Int a, Vector2Int b)
        {
            EnsureSets();
            _dynamicEdgeBlocks.Add(NormalizeEdge(a, b));
        }
        public void AddDynamicEdgeBlock(Vector2Int a, EdgeDirection side)
        {
            EnsureSets();
            _dynamicEdgeBlocks.Add(NormalizeEdge(a, a + DirToVec(side)));
        }
        public void RemoveDynamicEdgeBlock(Vector2Int a, Vector2Int b)
        {
            EnsureSets();
            _dynamicEdgeBlocks.Remove(NormalizeEdge(a, b));
        }
        public void RemoveDynamicEdgeBlock(Vector2Int a, EdgeDirection side)
        {
            EnsureSets();
            _dynamicEdgeBlocks.Remove(NormalizeEdge(a, a + DirToVec(side)));
        }

        private static Vector2Int DirToVec(EdgeDirection d) => d switch
        {
            EdgeDirection.North => Vector2Int.up,    // (0, +1)
            EdgeDirection.East => Vector2Int.right, // (+1, 0)
            EdgeDirection.South => Vector2Int.down,  // (0, -1)
            EdgeDirection.West => Vector2Int.left,  // (-1, 0)
            _ => Vector2Int.zero
        };

        //--- Ear Noise ---
        private void OnEnable() { NoiseSystem.NoiseRaised += OnNoiseRaised; }
        private void OnDisable() { NoiseSystem.NoiseRaised -= OnNoiseRaised; }

        private void OnNoiseRaised(Vector2Int at)
        {
            if (_state == NMEState.HeroUnspotted)
            {
                _state = NMEState.HeroSpotted;
                _nextRepathAt = 0f;   // repath immédiat
            }
        }
    }
}
