// FILE: Assets/Dev/Scripts/NME/NMEController.cs
//
// Rôle (résumé)
// - Ennemi à grille : patrouille passive (HeroUnspotted) ? passe en poursuite (HeroSpotted) si FOV/LOS voit le Héros
//   ou en cas de bruit (NoiseSystem).
// - En poursuite : recalcule un chemin (BFS) vers la position du héros en respectant murs + “thin walls”,
//   avance step par step (lerp), rotation instantanée vers la prochaine direction.
// - Conflits : cède au Héros sur concurrence de case en début de step (heroYieldThreshold) ; arbitre
//   les conflits NME?NME via priorité InstanceID et fenêtres nmeYieldThreshold / nmeVacateThreshold.
// - Attaque télégrafiée quand adjacent : pré-délai, télégraphe visuel, impact ? déclenche Reset via ResetManager.
// - Implémente IResettable : reconstruit ses caches, réapplique les grilles fermées, se replace au spawn.
//
// Invariants
// - AUCUN renommage de champs sérialisés / propriétés publiques / méthodes publiques / signatures.
// - Logique strictement inchangée. On n’ajoute que des commentaires et des renommages **locaux** plus explicites.
// - Les appels externes (GridUtils, ResetManager, NoiseSystem, Gate/Door systems) restent identiques.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Sarabande.Core;
using Sarabande.Levels;
using Sarabande.Player; // HeroController
using static Sarabande.Core.GridUtils;
using LEM = LevelEntitiesManager;

namespace Sarabande.NME
{
    public enum NMEState { HeroUnspotted, HeroSpotted }

    /// <summary>
    /// Ennemi case-par-case : vision cône + LOS, poursuite BFS, gestion de conflits multi-acteurs
    /// et attaque télégrafiée au contact.
    /// </summary>
    public class NMEController : MonoBehaviour, IResettable, IActor
    {
        // ?????????????????????????????????????????????????????????????????????????????
        // Data & Refs (noms conservés)
        // ?????????????????????????????????????????????????????????????????????????????

        [Header("ActorSettings")]
        [SerializeField] private ActorType _actorType;
        ActorType IActor.type => _actorType;

        [SerializeField] private ActorInteractionData intentInteraction;
        [SerializeField] private ActorInteractionData moveInteraction;
        [SerializeField] private ActorInteractionData leaveInteraction;
        [SerializeField] private CardinalDirection actorDirection;

        [Header("Data & Refs")]
        [SerializeField] private Sarabande.Core.LevelContext levelContext;
        [SerializeField, HideInInspector] private Sarabande.Levels.LevelData levelData;
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
        [SerializeField] private CardinalDirection initialFacing = CardinalDirection.East;

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
        [SerializeField, Range(0f, 0.5f)] private float heroYieldThreshold = 0.15f;

        [Header("NME Conflict")]
        [Tooltip("Fenêtre initiale d’abandon si un autre NME vise la même case au même instant. Le plus petit InstanceID passe.")]
        [SerializeField, Range(0f, 0.5f)] private float nmeYieldThreshold = 0.15f;

        [Tooltip("Pendant un déplacement, tant que l’autre NME n’a pas quitté sa case d’origine au-delà de ce pourcentage, sa case reste considérée occupée.")]
        [SerializeField, Range(0f, 1f)] private float nmeVacateThreshold = 0.25f;

        [SerializeField] private ResetManager resetManager;  // à assigner (LevelRoot)

        // ?????????????????????????????????????????????????????????????????????????????
        // Runtime state (noms conservés)
        // ?????????????????????????????????????????????????????????????????????????????

        private NMEState _state = NMEState.HeroUnspotted;

        private GameObject _activeTelegraph;

        // spawn dynamique (0/1/N)
        private Vector2Int? _spawnOverrideCell = null;
        /// <summary>Fixe la cellule de spawn (utilisée à Start/Reset).</summary>
        public void SetSpawnCell(Vector2Int cell) => _spawnOverrideCell = cell;

        // positions/logique
        private Vector2Int _gridPos;
        private bool _isMoving = false;
        private bool _isAttacking = false;
        private float _readyAt = 0f;
        private float _nextRepathAt = 0f;

        private Vector2Int _lastGoal; // dernière case héros connue utilisée pour le path

        /// <summary>Vrai pendant l’interpolation d’un step.</summary>
        public bool IsStepping => _isMoving;
        /// <summary>Case de départ du step courant.</summary>
        public Vector2Int FromCell { get; private set; }
        /// <summary>Case d’arrivée du step courant.</summary>
        public Vector2Int ToCell { get; private set; }
        /// <summary>Progression du step 0..1.</summary>
        public float MoveProgress { get; private set; } // 0..1 pendant un step
        /// <summary>Position logique (grille).</summary>
        public Vector2Int GridPos => _gridPos;



        // collisions (murs / murs fins)
        private HashSet<Vector2Int> _blockedCells;
        private HashSet<(Vector2Int a, Vector2Int b)> _thinBlockers;
        private readonly HashSet<(Vector2Int a, Vector2Int b)> _dynamicEdgeBlocks = new();
        private readonly HashSet<Vector2Int> _dynamicBlockCells = new();

        // path courant (séquence de cases à suivre, exclut la case actuelle)
        private readonly List<Vector2Int> _path = new();

        // ?????????????????????????????????????????????????????????????????????????????
        // Unity lifecycle
        // ?????????????????????????????????????????????????????????????????????????????

        /// <summary>
        /// Prépare collisions, spawn, facing et état initial.
        /// </summary>
        private void Start()
        {
            if (hero == null) hero = FindFirstObjectByType<HeroController>(FindObjectsInactive.Include);
            if (resetManager == null) resetManager = FindFirstObjectByType<ResetManager>(FindObjectsInactive.Include);

            if (levelData == null || hero == null)
            {
                Debug.LogError("[NME] LevelData ou Hero manquant.");
                enabled = false;
                return;
            }

            moveInteraction = new ActorInteractionData(_actorType, ActorInteractionType.OnMove);
            leaveInteraction = new ActorInteractionData(_actorType, ActorInteractionType.OnLeave);
            intentInteraction = new ActorInteractionData(_actorType, ActorInteractionType.OnIntent);

            BuildCollisionSets();

            Vector2Int spawnCell;
            if (_spawnOverrideCell.HasValue)
            {
                spawnCell = _spawnOverrideCell.Value;
            }
            else
            {
                // migration vers 0/1/N spawns
                spawnCell = new Vector2Int(levelData.nmeSpawns[0].x, levelData.nmeSpawns[0].z);
            }

            _gridPos = spawnCell;
            transform.position = Center(_gridPos, cellSize);

            // init exposées
            FromCell = ToCell = _gridPos;
            MoveProgress = 0f;

            Sarabande.NME.NMEOccupancy.Register(this, _gridPos);

            // cache de la dernière cible
            _lastGoal = hero.GridPos;

            SetFacing(initialFacing);
            _state = NMEState.HeroUnspotted;

            _nextRepathAt = Time.time;
        }

        /// <summary>
        /// FSM de l’ennemi : scan FOV/LOS en Unspotted ; poursuite + attaque en Spotted.
        /// </summary>
        private void Update()
        {
            if (resetManager != null && resetManager.IsResetInProgress) return;

            switch (_state)
            {
                case NMEState.HeroUnspotted:
                    {
                        // immobile, scrute le cône
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
                                Vector2Int delta = heroPos - _gridPos;   // forcément cardinal
                                StartCoroutine(AttackRoutine(heroPos, delta));
                            }
                            return; // pas de déplacement pendant l’attaque
                        }

                        // Repath périodique / à la demande
                        bool goalChanged = heroPos != _lastGoal;
                        bool needRepath = (_path.Count == 0) || goalChanged;

                        if (needRepath || Time.time >= _nextRepathAt)
                        {
                            RecomputePath(heroPos);
                            _lastGoal = heroPos;
                            _nextRepathAt = Time.time + repathInterval;
                        }

                        // Avancer d'une case si chemin
                        if (_path.Count > 0)
                        {
                            var next = _path[0];

                            // s’assurer que 'next' est adjacent à la position actuelle
                            int manhattan = Mathf.Abs(next.x - _gridPos.x) + Mathf.Abs(next.y - _gridPos.y);
                            if (manhattan != 1)
                            {
                                // chemin obsolète (ex: reset) : recalcule
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
                            actorDirection = GetCardinalDirection(next - _gridPos);
                            // Gate fermée sur l’arête -> on annule et on repath
                            if (HasThinWallBetween(_gridPos, next))
                            {
                                _readyAt = Time.time + interStepPause;
                                _path.Clear();
                                _nextRepathAt = 0f; // repath immédiat
                                return;
                            }

                            StartCoroutine(StepTo(next));
                        }
                        else
                        {
                            // Aucun chemin possible (bouché)
                            _readyAt = Time.time + interStepPause;
                        }
                        return;
                    }
            }
        }

        // ?????????????????????????????????????????????????????????????????????????????
        // Détection (FOV/LOS) & bruit
        // ?????????????????????????????????????????????????????????????????????????????

        /// <summary>
        /// Retourne vrai si le Héros est dans le cône FOV et en ligne de vue non bloquée (layer Obstacles).
        /// </summary>
        private bool CanSeeHero()
        {
            Vector3 origin = transform.position + Vector3.up * losHeight;
            Vector3 target = hero.WorldPos + Vector3.up * losHeight;

            Vector3 toHero = target - origin;
            Vector3 toHeroXZ = new Vector3(toHero.x, 0f, toHero.z);
            if (toHeroXZ.sqrMagnitude < 0.0001f) return true; // même case presque

            // Test angle
            Vector3 fwdXZ = new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;
            float angle = Vector3.Angle(fwdXZ, toHeroXZ.normalized);
            if (angle > (fovDegrees * 0.5f)) return false;

            // Test LOS (murs + murs fins via obstaclesMask)
            float dist = toHero.magnitude;
            if (Physics.Raycast(origin, toHero / dist, dist, obstaclesMask))
                return false;

            return true;
        }

        /// <summary>Passage en poursuite après un bruit entendu.</summary>
        public void HeardNoise()
        {
            if (_state == NMEState.HeroUnspotted)
                _state = NMEState.HeroSpotted;
        }

        /// <summary>Vrai si le Héros se trouve “dans” la cellule (en coordonnées monde).</summary>
        private bool IsHeroInsideCell(Vector2Int cell)
        {
            Vector3 center = Center(cell, cellSize);
            float half = cellSize * 0.5f;
            Vector3 p = hero.WorldPos;

            float eps = 0.001f;
            bool insideX = p.x >= center.x - half + eps && p.x <= center.x + half - eps;
            bool insideZ = p.z >= center.z - half + eps && p.z <= center.z + half - eps;
            return insideX && insideZ;
        }

        // ?????????????????????????????????????????????????????????????????????????????
        // Déplacements & pathfinding
        // ?????????????????????????????????????????????????????????????????????????????

        /// <summary>
        /// Step interpolé vers <paramref name="target"/> (avec fenêtres de cession Héros/NME
        /// et vérifs de “thin walls” pendant la transition).
        /// </summary>
        private IEnumerator StepTo(Vector2Int target)
        {
            _isMoving = true;

            FromCell = _gridPos;
            ToCell = target;
            MoveProgress = 0f;

            // --- OCCUPATION PAR D'AUTRES NME (photo à t=0) ---
            // 1) Si un autre NME est déjà "posé" sur target -> on n'avance pas
            if (IsCellOccupiedNowByNME(target))
            {
                _isMoving = false;
                _readyAt = Time.time + interStepPause;
                _path.Clear();
                _nextRepathAt = 0f;   // repath asap
                yield break;
            }

            // 2) Conflit simultané : deux NME veulent "target" au même frame
            if (SomeoneSteppingTo(target, out var otherStepping) && !WinsTieFor(target))
            {
                _isMoving = false;
                _readyAt = Time.time + interStepPause;
                yield break;
            }

            // --- Gate fermée entre ma case et la cible ---
            if (HasThinWallBetween(_gridPos, target))
            {
                _isMoving = false;
                _readyAt = Time.time + interStepPause;
                _path.Clear();
                _nextRepathAt = 0f;
                yield break;
            }

            Vector3 start = transform.position;
            Vector3 end = Center(target, cellSize);

            // Héros vise la même case ? fenêtre de cession Héros
            bool checkYieldToHero = (hero != null && hero.IsStepping && target == hero.ToCell);

            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / stepDuration;
                MoveProgress = t;

                // fenêtre de cession inter-NME
                if (t <= nmeYieldThreshold && SomeoneSteppingTo(ToCell, out var otherNow))
                {
                    if (otherNow && otherNow.GetInstanceID() < this.GetInstanceID())
                    {
                        // annule le step proprement
                        transform.position = start;
                        _isMoving = false;
                        MoveProgress = 0f;
                        FromCell = ToCell = _gridPos;
                        _readyAt = Time.time + interStepPause;
                        _path.Clear();
                        _nextRepathAt = 0f;
                        yield break;
                    }
                }

                // fenêtre de cession au Héros (tout début de step)
                if (checkYieldToHero && t <= heroYieldThreshold)
                {
                    transform.position = start;        // snap back immédiat
                    _isMoving = false;
                    _readyAt = Time.time + interStepPause;
                    _path.Clear();
                    _nextRepathAt = 0f;
                    yield break;
                }

                if (t > 1f) t = 1f;
                transform.position = Vector3.Lerp(start, end, t);
                yield return null;
            }

            moveInteraction.UpdateInteraction(actorDirection,ToCell);
            leaveInteraction.UpdateInteraction(actorDirection,FromCell);
            ActorEvents.NotifyActorMove(this, leaveInteraction);

            _gridPos = target;
            _isMoving = false;
            _readyAt = Time.time + interStepPause;

            ActorEvents.NotifyActorMove(this,moveInteraction);


            MoveProgress = 0f;
            FromCell = ToCell = _gridPos;

            Sarabande.NME.NMEOccupancy.UpdateCell(this, _gridPos);
        }

        /// <summary>Recalcule un chemin BFS vers <paramref name="goal"/> (respecte murs & thin walls).</summary>
        private void RecomputePath(Vector2Int goal)
        {
            _path.Clear();

            var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
            var frontier = new Queue<Vector2Int>();
            var visited = new HashSet<Vector2Int>();

            frontier.Enqueue(_gridPos);
            visited.Add(_gridPos);

            bool found = false;

            while (frontier.Count > 0)
            {
                var current = frontier.Dequeue();

                if (current == goal)
                {
                    found = true;
                    break;
                }

                foreach (var neighbor in NeighborsTowardGoal(current, goal))
                {
                    if (visited.Contains(neighbor)) continue;
                    visited.Add(neighbor);
                    cameFrom[neighbor] = current;
                    frontier.Enqueue(neighbor);
                }
            }

            if (!found) return;

            var cur = goal;
            var stack = new Stack<Vector2Int>();
            while (cur != _gridPos)
            {
                stack.Push(cur);
                if (!cameFrom.TryGetValue(cur, out cur))
                {
                    _path.Clear();
                    return;
                }
            }
            while (stack.Count > 0)
                _path.Add(stack.Pop());
        }

        /// <summary>
        /// Renvoie les voisins franchissables depuis <paramref name="c"/>, triés pour tendre vers <paramref name="goal"/>.
        /// </summary>
        private IEnumerable<Vector2Int> NeighborsTowardGoal(Vector2Int c, Vector2Int goal)
        {
            var dirs = new[]
            {
                Vector2Int.right, // tie-breaker stable: Est d'abord
                Vector2Int.left,
                Vector2Int.up,
                Vector2Int.down
            };

            var candidates = new List<(Vector2Int n, int dist, int tie)>(4);

            for (int i = 0; i < dirs.Length; i++)
            {
                var d = dirs[i];
                var n = c + d;
                if (!InsideBounds(n, levelData.width, levelData.height)) continue;
                if (_blockedCells.Contains(n)) continue;
                if (HasThinWallBetween(c, n)) continue;

                int dist = Mathf.Abs(n.x - goal.x) + Mathf.Abs(n.y - goal.y);
                candidates.Add((n, dist, i));
            }

            candidates.Sort((a, b) =>
            {
                int cmp = a.dist.CompareTo(b.dist);
                if (cmp != 0) return cmp;
                return a.tie.CompareTo(b.tie); // ordre stable Est>Ouest>Nord>Sud
            });

            foreach (var p in candidates) yield return p.n;
        }

        // ?????????????????????????????????????????????????????????????????????????????
        // Collisions / utilitaires
        // ?????????????????????????????????????????????????????????????????????????????

        /// <summary>Reconstruit les caches de collision à partir du LevelData + dynamiques.</summary>
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

        /// <summary>Vrai s’il existe un “thin wall” (statique ou dynamique) entre from et to.</summary>
        private bool HasThinWallBetween(Vector2Int from, Vector2Int to)
        {
            var key = NormalizeEdge(from, to);
            return _thinBlockers.Contains(key) || _dynamicEdgeBlocks.Contains(key);
        }

        /// <summary>Vrai si deux cases sont cardinalement adjacentes et non séparées par un thin wall.</summary>
        private bool IsDirectlyAdjacent(Vector2Int a, Vector2Int b)
        {
            var d = a - b;
            int md = Mathf.Abs(d.x) + Mathf.Abs(d.y);
            if (md != 1) return false;                 // pas cardinal / pas adjacent
            return !HasThinWallBetween(a, b);          // adjacent oui, mais pas à travers un thin wall
        }

        // ?????????????????????????????????????????????????????????????????????????????
        // Multi-NME occupancy helpers (REMIS)
        // ?????????????????????????????????????????????????????????????????????????????

        /// <summary>
        /// Vrai si un autre NME occupe déjà <paramref name="cell"/> maintenant
        /// (posé, ou en transition n’ayant pas assez libéré/quitté).
        /// </summary>
        private bool IsCellOccupiedNowByNME(Vector2Int cell)
        {
            var all = FindObjectsByType<NMEController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var other in all)
            {
                if (!other || other == this) continue;

                if (!other.IsStepping)
                {
                    // posé
                    if (other.GridPos == cell) return true;
                }
                else
                {
                    // OCCUPE les deux cases pendant la transition
                    if (other.ToCell == cell) return true;                          // destination “réservée”
                    if (other.FromCell == cell && other.MoveProgress < nmeVacateThreshold)
                        return true;                                                // pas assez “libéré” sa case d'origine
                }
            }
            return false;
        }

        /// <summary>Vrai si au moins un autre NME vise actuellement <paramref name="cell"/> (ToCell==cell).</summary>
        private bool SomeoneSteppingTo(Vector2Int cell, out NMEController first)
        {
            first = null;
            var all = FindObjectsByType<NMEController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var other in all)
            {
                if (!other || other == this) continue;
                if (other.IsStepping && other.ToCell == cell)
                {
                    first = other;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Priorité sur <paramref name="cell"/> : gagne si l’InstanceID de cet objet
        /// est le plus petit parmi ceux qui la visent.
        /// </summary>
        private bool WinsTieFor(Vector2Int cell)
        {
            int myId = GetInstanceID();
            var all = FindObjectsByType<NMEController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var other in all)
            {
                if (!other || other == this) continue;
                if (other.IsStepping && other.ToCell == cell)
                {
                    if (other.GetInstanceID() < myId) return false;
                }
            }
            return true;
        }

        // ?????????????????????????????????????????????????????????????????????????????
        // Facing / rotation
        // ?????????????????????????????????????????????????????????????????????????????

        private void SetFacing(CardinalDirection dir)
        {
            var fwd = DirToWorld(dir);
            if (fwd.sqrMagnitude > 0f)
                transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);
        }

        /// <summary>Applique une orientation initiale (utilisée par le spawner) et mémorise pour les resets.</summary>
        public void OverrideInitialFacing(CardinalDirection dir)
        {
            initialFacing = dir;
            SetFacing(dir); // applique visuellement dès maintenant
        }

        private void FaceDirection(Vector2Int delta)
        {
            if (delta == Vector2Int.right) SetFacing(CardinalDirection.East);
            else if (delta == Vector2Int.left) SetFacing(CardinalDirection.West);
            else if (delta == Vector2Int.up) SetFacing(CardinalDirection.North);
            else if (delta == Vector2Int.down) SetFacing(CardinalDirection.South);
        }

        // ?????????????????????????????????????????????????????????????????????????????
        // Reset (IResettable)
        // ?????????????????????????????????????????????????????????????????????????????

        /// <summary>
        /// Annule toute coroutine/attaque, réinitialise les caches et la position,
        /// réapplique les grilles fermées au NME et repasse en HeroUnspotted.
        /// </summary>
        public void ResetToInitial()
        {
            StopAllCoroutines();
            CancelAttackImmediate();
            MoveProgress = 0f;
            FromCell = ToCell = _gridPos;
            _isAttacking = false;
            _path.Clear();
            _isMoving = false;
            _readyAt = 0f;
            _nextRepathAt = Time.time;

            BuildCollisionSets();

            _dynamicEdgeBlocks.Clear();

            /*// Important : réappliquer les grilles fermées au NME fraîchement reset
            var gateSys = FindFirstObjectByType<Sarabande.Gates.GridGateSystem>(FindObjectsInactive.Include);
            if (gateSys != null)
                gateSys.ReapplyBlocksTo(this);
            */
            Vector2Int spawnCell;
            if (_spawnOverrideCell.HasValue)
            {
                spawnCell = _spawnOverrideCell.Value;
            }
            else
            {
                spawnCell = new Vector2Int(levelData.nmeSpawns[0].x, levelData.nmeSpawns[0].z);
            }

            _gridPos = spawnCell;
            transform.position = Center(_gridPos, cellSize);

            SetFacing(initialFacing);
            _state = NMEState.HeroUnspotted;

            Sarabande.NME.NMEOccupancy.UpdateCell(this, _gridPos);
        }

        // ?????????????????????????????????????????????????????????????????????????????
        // Telégraphie / Attaque
        // ?????????????????????????????????????????????????????????????????????????????

        /// <summary>Crée (et mémorise) un disque de télégraphe sur la cellule visée.</summary>
        private GameObject CreateTelegraphMarker(Vector2Int cell)
        {
            // Sécurité : jamais deux marqueurs en même temps
            DestroyTelegraphIfAny();

            var telegraphGO = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            telegraphGO.name = "AttackTelegraph";

            var colliderComponent = telegraphGO.GetComponent<Collider>();
            if (colliderComponent) Destroy(colliderComponent);

            var cellCenterWorld = Center(cell, cellSize);
            float halfHeight = 0.01f;
            telegraphGO.transform.position = new Vector3(cellCenterWorld.x, telegraphY + halfHeight, cellCenterWorld.z);
            telegraphGO.transform.localScale = new Vector3(telegraphDiameter, 0.01f, telegraphDiameter);
            telegraphGO.transform.SetParent(transform, true); // position monde

            var meshRenderer = telegraphGO.GetComponent<MeshRenderer>();
            if (meshRenderer)
            {
                meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                meshRenderer.receiveShadows = false;
                if (telegraphMaterial) meshRenderer.sharedMaterial = telegraphMaterial;
            }

            _activeTelegraph = telegraphGO;
            return telegraphGO;
        }

        /// <summary>
        /// Séquence d’attaque : pré-délai ? télégraphe ? impact (reset) ou recover si raté.
        /// </summary>
        private IEnumerator AttackRoutine(Vector2Int targetCell, Vector2Int dir)
        {
            _isAttacking = true;
            _path.Clear();          // on gèle la poursuite pendant l'attaque
            FaceDirection(dir);     // verrouille la direction dès le début

            // 1) Pré-délai
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

            // 2) Télégrafie (disque sur la case visée)
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

        /// <summary>Détruit le marqueur de télégraphe actif s’il existe.</summary>
        private void DestroyTelegraphIfAny()
        {
            if (_activeTelegraph)
            {
                Destroy(_activeTelegraph);
                _activeTelegraph = null;
            }
        }

        /// <summary>Annule immédiatement une attaque en cours (flag + télégraphe).</summary>
        private void CancelAttackImmediate()
        {
            _isAttacking = false;
            DestroyTelegraphIfAny();
        }

        // ?????????????????????????????????????????????????????????????????????????????
        // GIZMOS (FOV & LOS)
        // ?????????????????????????????????????????????????????????????????????????????

        private void OnDrawGizmosSelected()
        {
            if (!drawFovGizmos) return;

            // Couleurs
            Color colFov = new Color(1f, 0.92f, 0.016f, 0.6f);  // jaune
            Color colLosOk = new Color(0.2f, 1f, 0.2f, 0.9f);   // vert
            Color colLosHit = new Color(1f, 0.2f, 0.2f, 0.9f);  // rouge

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

        // ?????????????????????????????????????????????????????????????????????????????
        // Blocs dynamiques (API identique)
        // ?????????????????????????????????????????????????????????????????????????????

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
        public void AddDynamicEdgeBlock(Vector2Int a, CardinalDirection side)
        {
            EnsureSets();
            _dynamicEdgeBlocks.Add(NormalizeEdge(a, a + DirToVec2(side)));
        }
        public void RemoveDynamicEdgeBlock(Vector2Int a, Vector2Int b)
        {
            EnsureSets();
            _dynamicEdgeBlocks.Remove(NormalizeEdge(a, b));
        }
        public void RemoveDynamicEdgeBlock(Vector2Int a, CardinalDirection side)
        {
            EnsureSets();
            _dynamicEdgeBlocks.Remove(NormalizeEdge(a, a + DirToVec2(side)));
        }

        // ?????????????????????????????????????????????????????????????????????????????
        // Events & LevelContext wiring
        // ?????????????????????????????????????????????????????????????????????????????

        private void OnEnable()
        {
            NoiseSystem.NoiseRaised += OnNoiseRaised;
            AttachContext();
        }

        private void OnDisable()
        {
            NoiseSystem.NoiseRaised -= OnNoiseRaised;
            DestroyTelegraphIfAny();
            //DetachContext();
            Sarabande.NME.NMEOccupancy.Unregister(this);
        }

        private void OnNoiseRaised(Vector2Int at)
        {
            if (_state == NMEState.HeroUnspotted)
            {
                _state = NMEState.HeroSpotted;
                _nextRepathAt = 0f;   // repath immédiat
            }
        }

        /// <summary>Abonnement au LevelContext (si présent) + init immédiate.</summary>
        private void AttachContext()
        {
            // if (!useLevelContext) return;  // laissé commenté comme dans la version source
            if (!levelContext)
                levelContext = GetComponentInParent<Sarabande.Core.LevelContext>();

            if (levelContext != null)
            {
                //levelContext.LevelDataChanged += HandleContextLevelDataChanged;
                HandleContextLevelDataChanged(levelContext.LevelData); // init immédiate
            }
            else
            {
                Debug.LogWarning($"[{GetType().Name}] Aucun LevelContext parent trouvé.");
            }
        }

        /// <summary>Se désabonne du LevelContext.</summary>


        /// <summary>Réagit au changement de LevelData : en jeu, reset complet.</summary>
        private void HandleContextLevelDataChanged(Sarabande.Levels.LevelData ld)
        {
            if (levelData == ld) return;
            levelData = ld;

#if UNITY_EDITOR
            if (!Application.isPlaying)
                UnityEditor.EditorUtility.SetDirty(this);
#endif

            // En jeu, on se replace proprement sur le nouveau LevelData
            if (Application.isPlaying && isActiveAndEnabled && levelData != null)
                ResetToInitial();
        }

#if UNITY_EDITOR
        private void OnValidate() { if (!Application.isPlaying) AttachContext(); }
#endif
    }
}
