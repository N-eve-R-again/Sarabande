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
    /// - Un pas = lerp vers la case voisine pendant 'stepDuration', puis courte pause 'interStepPause'.
    /// - Pour l'instant : pas de collisions murs/murs fins (on ajoute plus tard).
    /// </summary>
    [RequireComponent(typeof(PlayerInput))]
    public class HeroController : MonoBehaviour, Sarabande.Core.IResettable
    {
        [Header("Data")]
        [SerializeField] private LevelData levelData;
        [SerializeField, Min(0.001f)] private float cellSize = 1f;

        [Header("Movement")]
        [SerializeField, Min(0.01f)] private float stepDuration = 0.18f;   // à ajuster plus tard
        [SerializeField, Min(0f)] private float interStepPause = 0.04f;  // “micro arrêt” entre deux pas
        [SerializeField, Range(0.1f, 0.99f)] private float inputDeadzone = 0.5f;

        [Header("Collision & Bump")]
        [SerializeField, Min(0.01f)] private float bumpDistance = 0.12f;
        [SerializeField, Min(0.01f)] private float bumpOutDuration = 0.06f;
        [SerializeField, Min(0.01f)] private float bumpReturnDuration = 0.08f;

        [Header("Exit")]
        [SerializeField] private UnityEvent onExit;   // branché dans l’Inspector
        [SerializeField] private bool disableOnExit = true; // désactive le contrôle après victoire

        [Header("Facing")]
        [SerializeField] private bool faceOnMove = true;

        [SerializeField] private Sarabande.Core.ResetManager resetManager;

        // caches pour les collisions
        private HashSet<Vector2Int> _blockedCells; // non-walkables
        private HashSet<(Vector2Int a, Vector2Int b)> _thinBlockers; // murs fins normalisés


        private Vector2 _held;                 // dernier input maintenu (x,y)
        private bool _isMoving = false;
        private float _readyAtTime = 0f;       // quand un nouveau pas est autorisé

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

            // ? ICI : on prépare les sets de collision
            BuildCollisionSets(); 

            // Coord grille du spawn (ex. D8)
            _gridPos = new Vector2Int(levelData.heroSpawn.x, levelData.heroSpawn.z);

            // Centre monde de la case de spawn
            Vector3 spawnCenter = GridCenter(_gridPos);

            // Position initiale : une case "à l'extérieur" depuis la direction choisie
            Vector3 outside = spawnCenter + EntryOffset(levelData.heroEntry);
            transform.position = outside;

            // Lancer l'entry step (on bloque toute autre entrée pendant ce step)
            _isMoving = true;                 // pour éviter toute tentative de step concurrent
            StartCoroutine(SpawnInFromEdge()); // va appeler StepTo(_gridPos)
        }

        private void Update()
        {
            if (resetManager != null && resetManager.IsResetInProgress) return;
            if (_isMoving) return;                                      //step en cours, on attend
            if (Time.time < _readyAtTime) return;                       //micro-pause entre chaque step pas encore finie

            // Dir cardinal depuis l'input maintenu
            Vector2Int dir = HeldToCardinal(_held, inputDeadzone);
            if (dir == Vector2Int.zero) return;                         // si pas d'intention claire (avec un joystick par exemple)

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
            // Lance le pas
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

            // “micro arrêt” entre deux pas (même si la touche reste maintenue)
            _readyAtTime = Time.time + interStepPause;

            if (isExitMove)
            {
                // Déclenche la victoire à la fin du step de sortie
                onExit?.Invoke();
                if (disableOnExit) enabled = false; // stoppe ce controller pour figer le contrôle
            }
        }

        /// <summary>Reçoit l'action "Move" (PlayerInput = Send Messages).</summary>
        private void OnMove(InputValue value)
        {
            _held = value.Get<Vector2>(); // ex: (1,0), (-1,0), (0,1), (0,-1) ou un mix
            // Pas de lancement immédiat ici : Update() gère l’enchaînement avec interStepPause.
        }

        // --- Utilitaires ---

        private Vector3 GridCenter(Vector2Int c)
        {
            // Sol à Y=0 ; on centre dans la case (x+0.5, z+0.5)
            return new Vector3((c.x + 0.5f) * cellSize, 0f, (c.y + 0.5f) * cellSize);
        }

        private bool InsideBounds(Vector2Int c)
        {
            return c.x >= 0 && c.x < levelData.width && c.y >= 0 && c.y < levelData.height;
        }

        /// <summary>Convertit un Vector2 en direction cardinale (priorité à l’axe dominant).</summary>
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
            // 1 case en dehors de la grille dans la direction d'entrée
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
            yield return StepTo(_gridPos);  // joue exactement un step jusqu'au spawn
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
            // ordre lexicographique pour que (a,b) == (b,a)
            if (a.x < b.x) return (a, b);
            if (a.x > b.x) return (b, a);
            // a.x == b.x -> compare y (z)
            return (a.y <= b.y) ? (a, b) : (b, a);
        }

        private bool HasThinWallBetween(Vector2Int from, Vector2Int to)
        {
            // nos steps sont toujours cardinaux et adjacents ; on normalise et on teste
            var key = NormalizeEdge(from, to);
            return _thinBlockers.Contains(key);
        }
        private System.Collections.IEnumerator Bump(Vector2Int dir)
        {
            if (_isMoving) yield break; // sécurité
            _isMoving = true;

            Vector3 start = transform.position;
            Vector3 push = new Vector3(dir.x, 0f, dir.y).normalized * bumpDistance;

            // aller (rapide)
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / bumpOutDuration;
                if (t > 1f) t = 1f;
                transform.position = Vector3.Lerp(start, start + push, t);
                yield return null;
            }

            // retour (un peu plus “mou” si tu veux)
            t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / bumpReturnDuration;
                if (t > 1f) t = 1f;
                transform.position = Vector3.Lerp(start + push, start, t);
                yield return null;
            }

            transform.position = start; // verrouille exactement
            _isMoving = false;
            _readyAtTime = Time.time + interStepPause; // évite le spam
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
            // Stopper tout mouvement en cours
            StopAllCoroutines();
            enabled = true;                 // au cas où il avait été désactivé (après sortie)
            _held = Vector2.zero;
            _isMoving = false;
            _readyAtTime = 0f;

            // Recréer les caches collision (utile si, plus tard, le niveau évolue dynamiquement)
            BuildCollisionSets();

            // Revenir au spawn + rejouer l'entry step depuis le bord configuré
            _gridPos = new Vector2Int(levelData.heroSpawn.x, levelData.heroSpawn.z);
            var spawnCenter = GridCenter(_gridPos);
            var outside = spawnCenter + EntryOffset(levelData.heroEntry);
            transform.position = outside;

            _isMoving = true;
            StartCoroutine(SpawnInFromEdge()); // joue un step jusqu'au spawn
        }

    }
}
