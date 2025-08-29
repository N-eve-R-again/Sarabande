// FILE: Assets/DEV/Scripts/Player/HeroController.cs
using UnityEngine;
using UnityEngine.InputSystem;
using Sarabande.Core;
using Sarabande.Levels;
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

        [Header("Exit")]
        [SerializeField] private UnityEvent onExit;
        [SerializeField] private bool disableOnExit = true;

        [Header("Facing")]
        [SerializeField] private bool faceOnMove = true;

        [SerializeField] private Sarabande.Core.ResetManager resetManager;

        // caches collisions
        private HashSet<Vector2Int> _blockedCells; // non-walkables
        private HashSet<(Vector2Int a, Vector2Int b)> _thinBlockers; // murs fins normalisés

        private Vector2 _held;                 // dernier input maintenu (x,y)
        private bool _isMoving = false;
        private float _readyAtTime = 0f;       // quand un nouveau step est autorisé

        // Position logique sur la grille
        private Vector2Int _gridPos;
        public Vector2Int GridPos => _gridPos;

        public Vector3 WorldPos => transform.position;

        private void Start()
        {
            if (levelData == null)
            {
                Debug.LogError("[HeroController] LevelData manquant.");
                enabled = false;
                return;
            }

            BuildCollisionSets();

            // Coord grille du spawn (ex. D8)
            _gridPos = new Vector2Int(levelData.heroSpawn.x, levelData.heroSpawn.z);

            // Centre monde de la case de spawn
            Vector3 spawnCenter = GridCenter(_gridPos);

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
            if (_isMoving) return;
            if (Time.time < _readyAtTime) return;

            // Dir cardinal depuis l'input maintenu
            Vector2Int dir = HeldToCardinal(_held, inputDeadzone);
            if (dir == Vector2Int.zero) return;

            // Cible dans les bornes du niveau
            Vector2Int target = _gridPos + dir;

            // Sortie spéciale : autoriser à sortir hors-grille depuis la case/direction d'Exit
            if (IsExitMove(_gridPos, dir))
            {
                StartCoroutine(StepTo(target, isExitMove: true));
                return;
            }

            // 1) hors-grille -> bump
            if (!InsideBounds(target))
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

            Vector3 start = transform.position;
            Vector3 end = GridCenter(target);

            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / stepDuration;
                if (t > 1f) t = 1f;
                transform.position = Vector3.Lerp(start, end, t);
                yield return null;
            }

            _gridPos = target;
            _isMoving = false;

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
        }

        // --- Utilitaires ---

        private Vector3 GridCenter(Vector2Int c)
        {
            return new Vector3((c.x + 0.5f) * cellSize, 0f, (c.y + 0.5f) * cellSize);
        }

        private bool InsideBounds(Vector2Int c)
        {
            return c.x >= 0 && c.x < levelData.width && c.y >= 0 && c.y < levelData.height;
        }

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
        {
            switch (dir)
            {
                case EdgeDirection.North: return new Vector3(0f, 0f, cellSize);
                case EdgeDirection.East: return new Vector3(cellSize, 0f, 0f);
                case EdgeDirection.South: return new Vector3(0f, 0f, -cellSize);
                case EdgeDirection.West: return new Vector3(-cellSize, 0f, 0f);
                default: return Vector3.zero;
            }
        }

        private System.Collections.IEnumerator SpawnInFromEdge()
        {
            yield return StepTo(_gridPos);
        }

        private void BuildCollisionSets()
        {
            _blockedCells = new HashSet<Vector2Int>();
            foreach (var c in levelData.nonWalkables)
                _blockedCells.Add(new Vector2Int(c.x, c.z));

            _thinBlockers = new HashSet<(Vector2Int, Vector2Int)>();
            foreach (var e in levelData.thinWalls)
            {
                var a = new Vector2Int(e.a.x, e.a.z);
                var b = new Vector2Int(e.b.x, e.b.z);
                var key = NormalizeEdge(a, b);
                _thinBlockers.Add(key);
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
            return _thinBlockers.Contains(key);
        }

        private System.Collections.IEnumerator Bump(Vector2Int dir)
        {
            if (_isMoving) yield break;
            _isMoving = true;

            Vector3 start = transform.position;
            Vector3 push = new Vector3(dir.x, 0f, dir.y).normalized * bumpDistance;

            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / bumpOutDuration;
                if (t > 1f) t = 1f;
                transform.position = Vector3.Lerp(start, start + push, t);
                yield return null;
            }

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

        private static Vector2Int DirToVec(EdgeDirection dir)
        {
            return dir switch
            {
                EdgeDirection.North => Vector2Int.up,
                EdgeDirection.East => Vector2Int.right,
                EdgeDirection.South => Vector2Int.down,
                EdgeDirection.West => Vector2Int.left,
                _ => Vector2Int.zero
            };
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
            enabled = true;
            _held = Vector2.zero;
            _isMoving = false;
            _readyAtTime = 0f;

            BuildCollisionSets();

            _gridPos = new Vector2Int(levelData.heroSpawn.x, levelData.heroSpawn.z);
            var spawnCenter = GridCenter(_gridPos);
            var outside = spawnCenter + EntryOffset(levelData.heroEntry);
            transform.position = outside;

            _isMoving = true;
            StartCoroutine(SpawnInFromEdge());
        }
    }
}
