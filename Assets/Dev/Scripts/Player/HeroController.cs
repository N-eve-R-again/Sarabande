// FILE: Assets/DEV/Scripts/Player/HeroController.cs
using UnityEngine;
using UnityEngine.InputSystem;
using Sarabande.Core;
using Sarabande.Levels;
using Sarabande.NME;
using static Sarabande.Core.GridUtils;
using System.Collections.Generic;
using UnityEngine.Events;

namespace Sarabande.Player
{
    /// <summary>
    /// Déplacement case par case du héros, en temps réel.
    /// - Lit l'action "Move" (Vector2) du New Input System via PlayerInput (Send Messages).
    /// - Un step = lerp vers la case voisine pendant 'stepDuration', puis courte pause 'interStepPause'.
    /// - Collisions : hors-grille, murs, murs fins -> bump punitif.
    /// - Sortie : autorisée si on part depuis la case/direction configurée dans LevelData.
    /// </summary>
    [RequireComponent(typeof(PlayerInput))]
    public class HeroController : MonoBehaviour, Sarabande.Core.IResettable
    {
        [Header("Data")]
        [SerializeField] private LevelData levelData;
        [SerializeField, Min(0.001f)] private float cellSize = 1f;

        [Header("Movement")]
        [SerializeField, Min(0.01f)] private float stepDuration = 0.18f;
        [SerializeField, Min(0f)] private float interStepPause = 0.04f;
        [SerializeField, Range(0.1f, 0.99f)] private float inputDeadzone = 0.5f;

        [Header("Collision & Bump")]
        [SerializeField, Min(0.01f)] private float bumpDistance = 0.12f;
        [SerializeField, Min(0.01f)] private float bumpOutDuration = 0.06f;
        [SerializeField, Min(0.01f)] private float bumpReturnDuration = 0.08f;

        [Header("NME Conflict")]
        [SerializeField, Range(0f, 0.5f)] private float nmeYieldThreshold = 0.15f;   // priorité Héro si NME.t ? seuil
        [SerializeField, Range(0f, 0.5f)] private float nmeVacateThreshold = 0.25f;  // case NME quittée reste “occupée” tant que t < seuil
        private NMEController[] _nmes;

        [Header("Exit")]
        [SerializeField] private UnityEvent onExit;
        [SerializeField] private bool disableOnExit = true;

        [Header("Facing")]
        [SerializeField] private bool faceOnMove = true;

        [Header("Anti-overlap Guard")]
        [SerializeField] private bool enforceNoOverlap = true;
        [SerializeField, Min(0.01f)] private float overlapReturnDuration = 0.08f;
        [SerializeField, Range(0f, 0.5f)] private float overlapEarlyCheckFromT = 0.15f;
        // on commence à vérifier à partir de 15% du step (évite les faux positifs très tôt)


        [SerializeField] private Sarabande.Core.ResetManager resetManager;

        // caches collisions
        private HashSet<Vector2Int> _blockedCells; // non-walkables
        private HashSet<(Vector2Int a, Vector2Int b)> _thinBlockers; // murs fins normalisés
        private HashSet<(Vector2Int a, Vector2Int b)> _dynamicEdgeBlocks
            = new HashSet<(Vector2Int, Vector2Int)>();
        private HashSet<Vector2Int> _dynamicBlockCells = new HashSet<Vector2Int>();


        private Vector2 _held;                 // dernier input maintenu (x,y)
        private bool _isMoving = false;
        private float _readyAtTime = 0f;       // quand un nouveau step est autorisé

        public bool IsStepping => _isMoving;
        public Vector2Int FromCell { get; private set; }
        public Vector2Int ToCell { get; private set; }
        public float MoveProgress { get; private set; } // 0..1 pendant un step

        // Position logique sur la grille
        private Vector2Int _gridPos;
        public Vector2Int GridPos => _gridPos;
        public Vector3 WorldPos => transform.position;
        // pour notifier un seul input lors de l'activation d'un levier par exemple
        public Vector2Int CurrentIntentDir { get; private set; } = Vector2Int.zero;

        private void Start()
        {
            if (levelData == null)
            {
                Debug.LogError("[HeroController] LevelData manquant.");
                enabled = false;
                return;
            }

            BuildCollisionSets();

            _nmes = FindObjectsByType<NMEController>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            // Coord grille du spawn (ex. D8)
            _gridPos = new Vector2Int(levelData.heroSpawn.x, levelData.heroSpawn.z);

            // Centre monde de la case de spawn
            Vector3 spawnCenter = Center(_gridPos, cellSize);

            // Position initiale : une case "à l'extérieur" depuis la direction choisie
            Vector3 outside = spawnCenter + EntryOffset(levelData.heroEntry);
            transform.position = outside;

            // Lancer l'entry step
            _isMoving = true;
            StartCoroutine(SpawnInFromEdge()); // joue un step jusqu'au spawn
        }

        private void Update()
        {
            if (resetManager != null && resetManager.IsResetInProgress) return;

            Vector2Int dir = HeldToCardinal(_held, inputDeadzone);
            CurrentIntentDir = dir;

            if (_isMoving) return;
            if (Time.time < _readyAtTime) return;

            // Dir cardinal depuis l'input maintenu
            
            if (dir == Vector2Int.zero) return;

            // Cible dans les bornes du niveau
            Vector2Int target = _gridPos + dir;

            // Sortie spéciale : autoriser à sortir hors-grille depuis la case/direction d'Exit
            if (IsExitMove(_gridPos, dir))
            {
                // NEW: si une gate bloque l'arête de sortie, on bump au lieu de sortir
                if (HasThinWallBetween(_gridPos, target))   // <= utilise 'target' existant
                {
                    StartCoroutine(Bump(dir));
                    return;
                }

                StartCoroutine(StepTo(target, isExitMove: true));
                return;
            }

            // 1) hors-grille -> bump
            if (!InsideBounds(target, levelData.width, levelData.height))
            {
                StartCoroutine(Bump(dir));
                return;
            }

            // 2) case mur -> bump
            if (_blockedCells.Contains(target))
            {
                StartCoroutine(Bump(dir));
                return;
            }

            // 3) mur fin entre les deux cases -> bump
            if (HasThinWallBetween(_gridPos, target))
            {
                StartCoroutine(Bump(dir));
                return;
            }

            if (!CanEnterCellConsideringNME(target, dir))
            {
                StartCoroutine(Bump(dir));
                return;
            }

            if (faceOnMove) FaceDirection(dir);
            StartCoroutine(StepTo(target));
        }

        private void FaceDirection(Vector2Int dir)
        {
            if (dir == Vector2Int.zero) return;
            Vector3 fwd = new Vector3(dir.x, 0f, dir.y);
            transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);
        }

        private System.Collections.IEnumerator StepTo(Vector2Int target, bool isExitMove = false)
        {
            _isMoving = true;

            FromCell = _gridPos;
            ToCell = target;
            MoveProgress = 0f;

            Vector3 start = transform.position;
            Vector3 end = Center(target, cellSize);

            float t = 0f;
            while (t < 1f)
            {
                // progression anim
                t += Time.deltaTime / stepDuration;
                if (t > 1f) t = 1f;
                MoveProgress = t;

                // position prévue à cette frame
                Vector3 pos = Vector3.Lerp(start, end, t);

                // --- GARDE-FOU INTERMÉDIAIRE ---
                // Dès que la case devient réellement "occupée" (au sens des seuils),
                // on annule l’atterrissage et on revient vers la case d’origine.
                if (enforceNoOverlap && t >= overlapEarlyCheckFromT && IsCellOccupiedNowByNME(target))
                {
                    // retour depuis la position courante 'pos' vers 'start'
                    float rt = 0f;
                    while (rt < 1f)
                    {
                        rt += Time.deltaTime / overlapReturnDuration;
                        if (rt > 1f) rt = 1f;
                        transform.position = Vector3.Lerp(pos, start, rt);
                        yield return null;
                    }

                    transform.position = start;
                    _isMoving = false;
                    MoveProgress = 0f;
                    FromCell = ToCell = _gridPos;      // on reste logiquement sur la case d’origine
                    _readyAtTime = Time.time + interStepPause;
                    yield break;
                }

                // pas de conflit : on applique la position prévue
                transform.position = pos;
                yield return null;
            }


            // Anti-overlap final : si un NME occupe encore la case au moment d'atterrir, on rebondit
            if (enforceNoOverlap && IsCellOccupiedNowByNME(target))
            {
                // On a déjà start = position d'origine et end = centre de la case cible
                float rt = 0f;
                while (rt < 1f)
                {
                    rt += Time.deltaTime / overlapReturnDuration;
                    if (rt > 1f) rt = 1f;
                    // Retour visuel de 'end' (cible) vers 'start' (origine)
                    transform.position = Vector3.Lerp(end, start, rt);
                    yield return null;
                }

                transform.position = start;        // verrouille pile sur la case d'origine
                _isMoving = false;
                MoveProgress = 0f;
                FromCell = ToCell = _gridPos;      // reste sur la case d'origine côté logique
                _readyAtTime = Time.time + interStepPause;
                yield break;
            }

            _gridPos = target;
            _isMoving = false;

            MoveProgress = 0f;
            FromCell = ToCell = _gridPos;

            _readyAtTime = Time.time + interStepPause;

            if (isExitMove)
            {
                onExit?.Invoke();
                if (disableOnExit) enabled = false;
            }
        }

        private void OnMove(InputValue value)
        {
            _held = value.Get<Vector2>();
            CurrentIntentDir = HeldToCardinal(_held, inputDeadzone);
        }

        // --- Utilitaires ---

        private static Vector2Int HeldToCardinal(Vector2 v, float deadzone)
        {
            if (v.sqrMagnitude < deadzone * deadzone) return Vector2Int.zero;

            float ax = Mathf.Abs(v.x);
            float ay = Mathf.Abs(v.y);

            if (ax > ay)
                return v.x > 0f ? Vector2Int.right : Vector2Int.left;
            else
                return v.y > 0f ? Vector2Int.up : Vector2Int.down;
        }

        private Vector3 EntryOffset(EdgeDirection dir)
            => DirToWorld(dir) * cellSize;

        private System.Collections.IEnumerator SpawnInFromEdge()
        {
            yield return StepTo(_gridPos);
        }

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
                var key = NormalizeEdge(a, b);
                _thinBlockers.Add(key);
            }
        }

        private bool HasThinWallBetween(Vector2Int from, Vector2Int to)
        {
            var key = NormalizeEdge(from, to);
            bool thin = _thinBlockers.Contains(key);             // ton test existant
            bool dyn = _dynamicEdgeBlocks.Contains(key);        // AJOUT
            return thin || dyn;
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


        private bool CanEnterCellConsideringNME(Vector2Int target, Vector2Int dir)
        {
            if (_nmes == null) return true;

            foreach (var n in _nmes)
            {
                if (n == null) continue;

                // 1) NME immobile posé sur la cible -> bloque
                if (!n.IsStepping && n.GridPos == target)
                    return false;

                if (n.IsStepping)
                {
                    var from = n.FromCell;
                    var to = n.ToCell;
                    float t = n.MoveProgress;

                    // 2) Swap interdit : H vise 'from_N' et N vise 'ma case'
                    if (to == _gridPos && from == target)
                        return false;

                    // 3) NME entre dans 'target'
                    if (to == target)
                    {
                        // Héro prioritaire si NME pas assez engagé (il cédera lui-même)
                        if (t > nmeYieldThreshold)
                            return false; // trop avancé => bump
                        else
                            continue;     // autorisé
                    }

                    // 4) NME quitte 'target'
                    if (from == target)
                    {
                        if (t < nmeVacateThreshold)
                            return false; // n'a pas encore libéré la case
                        else
                            continue;     // ok, la case est suffisamment libérée
                    }
                }
            }

            return true; // personne ne bloque
        }
        private bool IsCellOccupiedNowByNME(Vector2Int target)
        {
            if (_nmes == null) return false;

            foreach (var n in _nmes)
            {
                if (n == null) continue;

                // NME immobile déjà sur la cible ? occupée
                if (!n.IsStepping && n.GridPos == target)
                    return true;

                if (n.IsStepping)
                {
                    // NME entre dans la cible : s'il est suffisamment engagé, on considère la case occupée
                    if (n.ToCell == target && n.MoveProgress > nmeYieldThreshold)
                        return true;

                    // NME quitte la cible : tant qu'il n'a pas assez libéré, la case reste occupée
                    if (n.FromCell == target && n.MoveProgress < nmeVacateThreshold)
                        return true;
                }
            }
            return false;
        }

        private System.Collections.IEnumerator Bump(Vector2Int dir)
        {
            if (_isMoving) yield break;
            _isMoving = true;

            // NEW: on se tourne vers la direction tentée, même si c'est bloqué
            if (faceOnMove) FaceDirection(dir);

            Vector3 start = transform.position;
            Vector3 push = new Vector3(dir.x, 0f, dir.y).normalized * bumpDistance;

            // Aller (rapide)
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / bumpOutDuration;
                if (t > 1f) t = 1f;
                transform.position = Vector3.Lerp(start, start + push, t);
                yield return null;
            }

            // Retour (un peu plus “mou” si tu veux)
            t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / bumpReturnDuration;
                if (t > 1f) t = 1f;
                transform.position = Vector3.Lerp(start + push, start, t);
                yield return null;
            }

            transform.position = start;
            _isMoving = false;
            _readyAtTime = Time.time + interStepPause;
        }

        private bool IsExitMove(Vector2Int from, Vector2Int dir)
        {
            var exit = levelData.exit;
            var exitCell = new Vector2Int(exit.fromCell.x, exit.fromCell.z);
            return from == exitCell && dir == DirToVec(exit.direction);
        }

        public void ResetToInitial()
        {
            StopAllCoroutines();
            MoveProgress = 0f;
            FromCell = ToCell = _gridPos;
            enabled = true;
            _held = Vector2.zero;
            _isMoving = false;
            _readyAtTime = 0f;

            // 1) VIDER d’abord les dynamiques (cells + edges)
            _dynamicBlockCells.Clear();
            _dynamicEdgeBlocks.Clear();

            // 2) Puis reconstruire les sets statiques à partir du LevelData
            BuildCollisionSets();

            // 3) (la GridGateSystem & TimedDoorSystem vont réinjecter leurs verrous tout de suite après leur propre Reset)
            _gridPos = new Vector2Int(levelData.heroSpawn.x, levelData.heroSpawn.z);
            var spawnCenter = Center(_gridPos, cellSize);
            var outside = spawnCenter + EntryOffset(levelData.heroEntry);
            transform.position = outside;

            _isMoving = true;
            StartCoroutine(SpawnInFromEdge());
        }
        private void EnsureSets()
        {
            if (_blockedCells == null) BuildCollisionSets();
        }
        public void AddDynamicBlockCell(Vector2Int c)
        {
            EnsureSets();
            _dynamicBlockCells.Add(c);
            _blockedCells.Add(c); // utile immédiatement, même si on ne rebuild pas
        }

        public void RemoveDynamicBlockCell(Vector2Int c)
        {
            EnsureSets();
            _dynamicBlockCells.Remove(c);

            // Si c'était un blocage purement dynamique, on le retire de _blockedCells.
            // S'il existe aussi en nonWalkables, on le laisse.
            bool isStatic = false;
            foreach (var gc in levelData.nonWalkables)
                if (gc.x == c.x && gc.z == c.y) { isStatic = true; break; }

            if (!isStatic) _blockedCells.Remove(c);
        }
    }
}
