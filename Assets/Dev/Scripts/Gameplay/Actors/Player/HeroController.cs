// FILE: Assets/DEV/Scripts/Player/HeroController.cs
//
// Rôle du script (résumé pour la passation)
// - Contrôle du déplacement case par case du héros sur une grille, en “temps réel court” (step + courte pause).
// - Lit l'action "Move" (Vector2) via le New Input System (PlayerInput en mode "Send Messages").
// - Gère : limites de niveau, murs pleins (nonWalkables), murs fins (thin walls / grid gates), conflits avec NME.
// - Déclenche la sortie uniquement si l'on part depuis la cellule et l'arête d’Exit définies dans LevelData.
// - S’intègre au cycle de Reset (IResettable) : le reset vient des systèmes externes (NME/ResetManager).
//
// Invariants (ne pas casser)
// - Aucun renommage de champs sérialisés, propriétés, événements, ni méthodes publiques.
// - Aucune modification de la logique métier ou des appels utilitaires existants.
// - Les quelques renommages de **variables locales** sont purement lisibles (portée limitée au bloc concerné).
//
// Dépendances
// - Sarabande.Core.GridUtils : utilitaires grille (InsideBounds, Center, DirToVec, NormalizeEdge, etc.)
// - Sarabande.Levels.LevelData : données de niveau (spawn, walls, thin walls, exit, etc.)
// - Sarabande.NME.NMEController : informations de progression NME pour éviter chevauchements et conflits.

using UnityEngine;
using UnityEngine.InputSystem;
using Sarabande.Core;
using Sarabande.Levels;
using Sarabande.NME;
using static Sarabande.Core.GridUtils;
using System.Collections.Generic;
using UnityEngine.Events;
using LEM = LevelEntitiesManager;
using Sarabande.Visuals;

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
    public class HeroController : MonoBehaviour, Sarabande.Core.IResettable, IActor
    {
        // ?????????????????????????????????????????????????????????????????????????????
        // Serialized fields (groupés par thème) — NOMS CONSERVÉS (NE PAS RENOMMER)
        // ?????????????????????????????????????????????????????????????????????????????

        [Header("ActorSettings")]
        [SerializeField] private ActorType _actorType;
        ActorType IActor.type => _actorType;

        //On prépare des ActorInteractionData pour les reutiliser (eviter le garbage collector)
        [SerializeField] private ActorInteractionData intentInteraction;
        [SerializeField] private ActorInteractionData moveInteraction;
        [SerializeField] private ActorInteractionData leaveInteraction;
        [SerializeField] private ActorInteractionData bumpInteraction;

        [SerializeField] private CardinalDirection actorDirection;

        [Header("Data")]
        [SerializeField] private bool useLevelContext = true;
        [SerializeField] private Sarabande.Core.LevelContext levelContext;
        [SerializeField, HideInInspector] private Sarabande.Levels.LevelData levelData;
        [SerializeField, Min(0.001f)] private float cellSize = 1f;

        [Header("Movement")]
        [SerializeField, Min(0.01f)] private float stepDuration = 0.18f;
        [SerializeField, Min(0f)] private float interStepPause = 0.04f;
        [SerializeField, Range(0.1f, 0.99f)] private float inputDeadzone = 0.5f;
        [SerializeField] private AnimationCurve stepToLerpCurve;

        [Header("Collision & Bump")]
        [SerializeField, Min(0.01f)] private float bumpDistance = 0.12f;
        [SerializeField, Min(0.01f)] private float bumpOutDuration = 0.06f;
        [SerializeField, Min(0.01f)] private float bumpReturnDuration = 0.08f;

        [Header("NME Conflict")]
        [SerializeField, Range(0f, 0.5f)] private float nmeYieldThreshold = 0.15f;   // priorité Héro si NME pas assez avancé sur son step
        [SerializeField, Range(0f, 0.5f)] private float nmeVacateThreshold = 0.25f;  // une case quittée par le NME reste occupée jusqu’au seuil
        private NMEController[] _nmes;

        [Header("Exit")]
        [SerializeField] private UnityEvent onExit;
        [SerializeField] private bool disableOnExit = true;

        [Header("Facing")]
        [SerializeField] private bool faceOnMove = true;
        [SerializeField] private CardinalSpriteVisual spriteVisual;

        [Header("Anti-overlap Guard")]
        [SerializeField] private bool enforceNoOverlap = true;
        [SerializeField, Min(0.01f)] private float overlapReturnDuration = 0.08f;
        [SerializeField, Range(0f, 0.5f)] private float overlapEarlyCheckFromT = 0.15f;
        [SerializeField, Range(0f, 1f)] private float validateMoveTime = 0.45f;
        // on commence à vérifier à partir de 15% du step (évite les faux positifs très tôt)

        [SerializeField] private Sarabande.Core.ResetManager resetManager;

        // ?????????????????????????????????????????????????????????????????????????????
        // Runtime caches / état (NOMS CONSERVÉS)
        // ?????????????????????????????????????????????????????????????????????????????

        // caches collisions
        private HashSet<Vector2Int> _blockedCells; // non-walkables
        private HashSet<(Vector2Int a, Vector2Int b)> _thinBlockers; // murs fins normalisés
        private HashSet<(Vector2Int a, Vector2Int b)> _dynamicEdgeBlocks = new HashSet<(Vector2Int, Vector2Int)>();
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

        // Dernière direction d’intention (ex. activation unique d’un levier)
        public Vector2Int CurrentIntentDir { get; private set; } = Vector2Int.zero;


        // ?????????????????????????????????????????????????????????????????????????????
        // Unity lifecycle
        // ?????????????????????????????????????????????????????????????????????????????

        /// <summary>
        /// Initialisation au démarrage : construit les sets de collisions, positionne le héros
        /// juste "hors" de la grille en fonction de l'entrée choisie, puis lance un step d’entrée.
        /// </summary>
        private void Start()
        {
            if (levelData == null)
            {
                Debug.LogError("[HeroController] LevelData manquant.");
                enabled = false;
                return;
            }

            moveInteraction = new ActorInteractionData(_actorType, ActorInteractionType.OnMove);
            bumpInteraction = new ActorInteractionData(_actorType, ActorInteractionType.OnBump);
            leaveInteraction = new ActorInteractionData(_actorType, ActorInteractionType.OnLeave);
            intentInteraction = new ActorInteractionData(_actorType, ActorInteractionType.OnIntent);

            BuildCollisionSets();

            _nmes = FindObjectsByType<NMEController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            ActorSpawn spawn = levelData.heroSpawnConfig;
            // Coord grille du spawn (ex. D8)
            _gridPos = (Vector2Int)spawn.spawnCell;

            // Centre monde de la case de spawn
            Vector3 spawnCenterWorld = Center(_gridPos, cellSize);

            // Position initiale : une case "à l'extérieur" depuis la direction choisie
            Vector3 outsideWorldPosition = spawnCenterWorld + EntryOffset(spawn.spawnDirection);
            transform.position = outsideWorldPosition;

            actorDirection = Opposite(spawn.spawnDirection);
            // Lancer l'entry step (jusqu’au centre de la cellule de spawn)
            _isMoving = true;
            StartCoroutine(SpawnInFromEdge());
        }

        /// <summary>
        /// Boucle par frame : lit l’input, applique tempo (pause), gère collisions & conflits NME,
        /// lance soit un bump, soit un step de déplacement, soit la sortie.
        /// </summary>
        private void Update()
        {
            if (resetManager != null && resetManager.IsResetInProgress) return;

            Vector2Int intendedDir = HeldToCardinal(_held, inputDeadzone);
            CurrentIntentDir = intendedDir;
            

            if (_isMoving) return;
            if (Time.time < _readyAtTime) return;
            if (intendedDir == Vector2Int.zero) return;

            // Case cible dans la grille
            Vector2Int targetCell = _gridPos + intendedDir;
            actorDirection = GetCardinalDirection(intendedDir);
            if (faceOnMove) FaceDirection();
            // Tentative de sortie : autorisée depuis la case/direction d'Exit
            if (IsExitMove(_gridPos, intendedDir))
            {
                // Si une gate bloque l'arête de sortie, on bump au lieu de sortir
                if (HasThinWallBetween(_gridPos, targetCell))
                {
                    StartCoroutine(Bump(intendedDir));
                    return;
                }

                StartCoroutine(StepTo(targetCell, isExitMove: true));
                return;
            }

            // 1) hors-grille -> bump
            if (!InsideBounds(targetCell, levelData.width, levelData.height))
            {
                StartCoroutine(Bump(intendedDir));
                return;
            }

            // 2) case mur -> bump
            if (_blockedCells.Contains(targetCell))
            {
                StartCoroutine(Bump(intendedDir));
                return;
            }

            // 3) mur fin entre les deux cases -> bump
            if (HasThinWallBetween(_gridPos, targetCell))
            {
                StartCoroutine(Bump(intendedDir));
                return;
            }

            // 4) règles NME (réservations/chevauchements)
            if (!CanEnterCellConsideringNME(targetCell, intendedDir))
            {
                StartCoroutine(Bump(intendedDir));
                return;
            }



            StartCoroutine(StepTo(targetCell));
        }

        /// <summary>
        /// Lorsqu’une action "Move" est reçue (Input System / Send Messages),
        /// on stocke la valeur analogique pour décision ultérieure.
        /// </summary>
        private void OnMove(InputValue value)
        {
            _held = value.Get<Vector2>();
            CurrentIntentDir = HeldToCardinal(_held, inputDeadzone);
        }

        private void OnEnable() { AttachContext(); }
        private void OnDisable() { DetachContext(); }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // En édition on (ré)attache le contexte pour garder les refs à jour
            if (!Application.isPlaying) AttachContext();
        }
#endif

        // ?????????????????????????????????????????????????????????????????????????????
        // Mouvement & collisions
        // ?????????????????????????????????????????????????????????????????????????????

        /// <summary>
        /// Oriente le transform dans la direction de déplacement (visuel/facing).
        /// </summary>
        private void FaceDirection()
        {
            spriteVisual.SetFacing(actorDirection);
        }

        /// <summary>
        /// Coroutine d’un step vers une cellule cible. Gère l’anti-overlap en cours et en fin de step.
        /// </summary>
        private System.Collections.IEnumerator StepTo(Vector2Int target, bool isExitMove = false)
        {
            _isMoving = true;

            FromCell = _gridPos;
            ToCell = target;

            intentInteraction.UpdateInteraction(actorDirection, target);
            moveInteraction.UpdateInteraction(actorDirection, ToCell);
            leaveInteraction.UpdateInteraction(actorDirection, FromCell);


            MoveProgress = 0f;
            bool actionvalidated = false;
            Vector3 worldStart = transform.position;
            Vector3 worldEnd = Center(target, cellSize);
            ActorEvents.NotifyActorMove(this, intentInteraction);
            float lerpT = 0f;
            while (lerpT < stepDuration)
            {
                // progression anim
                lerpT += Time.deltaTime;
                float ratio = lerpT / stepDuration;
                if (lerpT > stepDuration) lerpT = stepDuration;
                MoveProgress = ratio;

                if (ratio >= validateMoveTime && !actionvalidated)
                {
                    ActorEvents.NotifyActorMove(this, leaveInteraction);
                    ActorEvents.NotifyActorMove(this, moveInteraction);
                    actionvalidated = true;
                }
                    // position prévue à cette frame
                Vector3 worldPosAtThisFrame = Vector3.Lerp(worldStart, worldEnd, stepToLerpCurve.Evaluate(ratio));

                // --- GARDE-FOU INTERMÉDIAIRE ---
                // si on détecte une superposition en cours de step, on “revient” rapidement
                if (enforceNoOverlap && ratio >= overlapEarlyCheckFromT && IsCellOccupiedNowByNME(target))
                {
                    float returnLerpT = 0f;
                    while (returnLerpT < 1f)
                    {
                        returnLerpT += Time.deltaTime / overlapReturnDuration;
                        if (returnLerpT > 1f) returnLerpT = 1f;
                        transform.position = Vector3.Lerp(worldPosAtThisFrame, worldStart, returnLerpT);
                        yield return null;
                    }

                    transform.position = worldStart;
                    _isMoving = false;
                    MoveProgress = 0f;
                    
                    FromCell = ToCell = _gridPos;      // on reste logiquement sur la case d’origine
                    _readyAtTime = Time.time + interStepPause;
                    yield break;
                }


                // pas de conflit : on applique la position prévue
                transform.position = worldPosAtThisFrame;
                yield return null;
            }

            // Anti-overlap final (cas limites)
            if (enforceNoOverlap && IsCellOccupiedNowByNME(target))
            {
                float returnLerpT = 0f;
                while (returnLerpT < 1f)
                {
                    returnLerpT += Time.deltaTime / overlapReturnDuration;
                    if (returnLerpT > 1f) returnLerpT = 1f;
                    transform.position = Vector3.Lerp(worldEnd, worldStart, returnLerpT);
                    yield return null;
                }

                transform.position = worldStart;
                _isMoving = false;
                MoveProgress = 0f;
                FromCell = ToCell = _gridPos;
                _readyAtTime = Time.time + interStepPause;
                yield break;
            }

            // Step validé
            _gridPos = target;
            _isMoving = false;

            MoveProgress = 0f;
            FromCell = ToCell = _gridPos;

            _readyAtTime = Time.time + interStepPause;


            // Gestion de la sortie si ce step correspond à un “exit move”
            if (isExitMove)
            {
                onExit?.Invoke();
                if (disableOnExit) enabled = false; // coupe ce contrôleur pour éviter tout input post-sortie
            }

        }

        /// <summary>
        /// Petit aller-retour visuel en cas de blocage (mur/limite/conflit NME).
        /// </summary>
        private System.Collections.IEnumerator Bump(Vector2Int dir)
        {
            if (_isMoving) yield break;
            _isMoving = true;

            bumpInteraction.UpdateInteraction(actorDirection, _gridPos + dir);
            // on se tourne vers la direction tentée, même si c'est bloqué

            Vector3 start = transform.position;
            Vector3 bumpVector = new Vector3(dir.x, 0f, dir.y).normalized * bumpDistance;

            // Aller (rapide)
            float lerpT = 0f;
            while (lerpT < 1f)
            {
                lerpT += Time.deltaTime / bumpOutDuration;
                if (lerpT > 1f) lerpT = 1f;
                transform.position = Vector3.Lerp(start, start + bumpVector, lerpT);
                yield return null;
            }
            ActorEvents.NotifyActorMove( this, bumpInteraction);
            // Retour
            lerpT = 0f;
            while (lerpT < 1f)
            {
                lerpT += Time.deltaTime / bumpReturnDuration;
                if (lerpT > 1f) lerpT = 1f;
                transform.position = Vector3.Lerp(start + bumpVector, start, lerpT);
                yield return null;
            }

            transform.position = start;
            _isMoving = false;
            _readyAtTime = Time.time + interStepPause;
        }

        // ?????????????????????????????????????????????????????????????????????????????
        // Logique utilitaire
        // ?????????????????????????????????????????????????????????????????????????????

        /// <summary>
        /// Convertit une entrée analogique (Vector2) en direction cardinale (droite/gauche/haut/bas).
        /// </summary>
        private static Vector2Int HeldToCardinal(Vector2 inputVector, float deadzone)
        {
            if (inputVector.sqrMagnitude < deadzone * deadzone) return Vector2Int.zero;

            float absX = Mathf.Abs(inputVector.x);
            float absY = Mathf.Abs(inputVector.y);

            if (absX > absY)
                return inputVector.x > 0f ? Vector2Int.right : Vector2Int.left;
            else
                return inputVector.y > 0f ? Vector2Int.up : Vector2Int.down;
        }

        /// <summary>
        /// Décale d’une cellule vers l’extérieur de la grille selon l’edge d’entrée choisi.
        /// </summary>
        private Vector3 EntryOffset(CardinalDirection dir) => DirToWorld(dir) * cellSize;

        /// <summary>
        /// Step d’entrée depuis l’extérieur jusqu’à la cellule de spawn.
        /// </summary>
        private System.Collections.IEnumerator SpawnInFromEdge()
        {
            yield return StepTo(_gridPos);
        }

        /// <summary>
        /// Construit les sets de collisions : cellules bloquées et arêtes fines normalisées.
        /// </summary>
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

        /// <summary>
        /// Vrai si une arête fine (statique ou dynamique) bloque le passage entre deux cellules.
        /// </summary>
        private bool HasThinWallBetween(Vector2Int from, Vector2Int to)
        {
            var key = NormalizeEdge(from, to);
            bool thin = _thinBlockers.Contains(key);
            bool dyn = _dynamicEdgeBlocks.Contains(key);
            return thin || dyn;
        }

        /// <summary>
        /// Applique un blocage d’arête dynamique (par coordonnées exactes).
        /// </summary>
        public void AddDynamicEdgeBlock(Vector2Int a, Vector2Int b)
        {
            EnsureSets();
            _dynamicEdgeBlocks.Add(NormalizeEdge(a, b));
        }

        /// <summary>
        /// Applique un blocage d’arête dynamique (par côté depuis une cellule).
        /// </summary>
        public void AddDynamicEdgeBlock(Vector2Int a, CardinalDirection side)
        {
            EnsureSets();
            _dynamicEdgeBlocks.Add(NormalizeEdge(a, a + DirToVec(side)));
        }

        /// <summary>
        /// Retire un blocage d’arête dynamique (par coordonnées exactes).
        /// </summary>
        public void RemoveDynamicEdgeBlock(Vector2Int a, Vector2Int b)
        {
            EnsureSets();
            _dynamicEdgeBlocks.Remove(NormalizeEdge(a, b));
        }

        /// <summary>
        /// Retire un blocage d’arête dynamique (par côté depuis une cellule).
        /// </summary>
        public void RemoveDynamicEdgeBlock(Vector2Int a, CardinalDirection side)
        {
            EnsureSets();
            _dynamicEdgeBlocks.Remove(NormalizeEdge(a, a + DirToVec(side)));
        }

        /// <summary>
        /// Autorisation d’entrer dans la cellule cible selon l’état des NME (réservations/chevauchements).
        /// </summary>
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

        /// <summary>
        /// Vrai si la cellule cible est actuellement occupée par un NME (immobile ou suffisamment engagé/sortant).
        /// </summary>
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

        /// <summary>
        /// Détermine si le mouvement tenté correspond à une sortie valide (cellule + direction d’Exit).
        /// </summary>
        private bool IsExitMove(Vector2Int from, Vector2Int dir)
        {
            var exit = levelData;
            var exitCell = new Vector2Int(exit.exit.fromCell.x, exit.exit.fromCell.z);
            return from == exitCell && dir == DirToVec(exit.exit.direction);
        }

        // ?????????????????????????????????????????????????????????????????????????????
        // Gestion des blocs dynamiques (cellules)
        // ?????????????????????????????????????????????????????????????????????????????

        /// <summary>Assure l’existence des set de collisions (si appelés très tôt).</summary>
        private void EnsureSets()
        {
            if (_blockedCells == null) BuildCollisionSets();
        }

        /// <summary>Ajoute une cellule bloquante dynamique.</summary>
        public void AddDynamicBlockCell(Vector2Int c)
        {
            EnsureSets();
            _dynamicBlockCells.Add(c);
            _blockedCells.Add(c); // utile immédiatement sans rebuild complet
        }

        /// <summary>Retire une cellule bloquante dynamique (ne retire pas un blocage statique).</summary>
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

        // ?????????????????????????????????????????????????????????????????????????????
        // Reset (IResettable)
        // ?????????????????????????????????????????????????????????????????????????????

        /// <summary>
        /// Réinitialise l’état du héros pour le LevelData courant et relance le step d’entrée.
        /// Appelé par le ResetManager dans son cycle de reset.
        /// </summary>
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

            // 3) (la GridGateSystem & TimedDoorSystem vont réinjecter leurs verrous juste après leur propre Reset)
            _gridPos = new Vector2Int(levelData.heroSpawn.x, levelData.heroSpawn.z);
            var spawnCenter = Center(_gridPos, cellSize);
            var outside = spawnCenter + EntryOffset(levelData.heroEntry);
            transform.position = outside;

            _isMoving = true;
            StartCoroutine(SpawnInFromEdge());
        }

        // ?????????????????????????????????????????????????????????????????????????????
        // LevelContext wiring
        // ?????????????????????????????????????????????????????????????????????????????

        /// <summary>
        /// S’abonne au LevelContext (si utilisé) pour suivre les changements de LevelData.
        /// </summary>
        private void AttachContext()
        {
            if (!useLevelContext) return;

            if (!levelContext)
                levelContext = GetComponentInParent<Sarabande.Core.LevelContext>();

            if (levelContext != null)
            {
                levelContext.LevelDataChanged += HandleContextLevelDataChanged;
                HandleContextLevelDataChanged(levelContext.LevelData); // init immédiate
            }
            else
            {
                Debug.LogWarning($"[{GetType().Name}] Aucun LevelContext parent trouvé.");
            }
        }

        /// <summary>Se désabonne du LevelContext.</summary>
        private void DetachContext()
        {
            if (levelContext != null)
                levelContext.LevelDataChanged -= HandleContextLevelDataChanged;
        }

        /// <summary>
        /// Callback déclenchée lors d’un changement de LevelData dans le LevelContext.
        /// </summary>
        private void HandleContextLevelDataChanged(Sarabande.Levels.LevelData ld)
        {
            if (levelData == ld) return;
            levelData = ld;

#if UNITY_EDITOR
            if (!Application.isPlaying)
                UnityEditor.EditorUtility.SetDirty(this);
#endif

            // En jeu : se replacer sur le spawn du nouveau niveau
            if (Application.isPlaying && isActiveAndEnabled && levelData != null)
                ResetToInitial();
        }
    }
}
