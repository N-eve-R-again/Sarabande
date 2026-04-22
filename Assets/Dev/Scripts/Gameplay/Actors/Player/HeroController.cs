using UnityEngine;
using UnityEngine.InputSystem;

using Sarabande.Core;
using Sarabande.Levels;
using Sarabande.Actors;
using Sarabande.EntityExtensions;

using Sarabande.NME;
using static Sarabande.Core.GridUtils;

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
    public class HeroController : MonoBehaviour, IResettable, IActor, IInitializable
    {
        // ?????????????????????????????????????????????????????????????????????????????
        // Serialized fields (groupés par thème) — NOMS CONSERVÉS (NE PAS RENOMMER)
        // ?????????????????????????????????????????????????????????????????????????????

        [Header("ActorSettings")]
        [EnumButtons]
        [SerializeField] private ActorType _actorType;
        [SerializeField] private HeroData spawn;
        ActorType IActor.type => _actorType;

        //On prépare des ActorInteractionData pour les reutiliser (eviter le garbage collector)
        [SerializeField] private ActorInteractionData intentInteraction;
        [SerializeField] private ActorInteractionData moveInteraction;
        [SerializeField] private ActorInteractionData leaveInteraction;
        [SerializeField] private ActorInteractionData bumpInteraction;

        [SerializeField] private CardinalDirection actorDirection;

        [Header("Movement")]
        [SerializeField, Min(0.01f)] private float stepDuration = 0.18f;
        [SerializeField, Min(0f)] private float interStepPause = 0.04f;
        [SerializeField, Range(0.1f, 0.99f)] private float inputDeadzone = 0.5f;
        [SerializeField] private AnimationCurve stepToLerpCurve;
        [SerializeField] private AnimationCurve bumpLerpCurve;

        [Header("Collision & Bump")]
        [SerializeField, Min(0.01f)] private float bumpDistance = 0.12f;
        [SerializeField, Min(0.01f)] private float bumpDuration = 0.06f;
        [SerializeField, Min(0.01f)] private float bumpEventPing = 0.95f;


        [Header("NME Conflict")]
        [SerializeField, Range(0f, 0.5f)] private float nmeYieldThreshold = 0.15f;   // priorité Héro si NME pas assez avancé sur son step
        [SerializeField, Range(0f, 0.5f)] private float nmeVacateThreshold = 0.25f;  // une case quittée par le NME reste occupée jusqu’au seuil
        private NMEController[] _nmes;

        [Header("Facing")]
        [SerializeField] private bool faceOnMove = true;
        [SerializeField] private CardinalSpriteVisual spriteVisual;

        [Header("Anti-overlap Guard")]
        [SerializeField] private bool enforceNoOverlap = true;
        [SerializeField, Min(0.01f)] private float overlapReturnDuration = 0.08f;
        [SerializeField, Range(0f, 0.5f)] private float overlapEarlyCheckFromT = 0.15f;
        [SerializeField, Range(0f, 1f)] private float validateMoveTime = 0.45f;
        // on commence à vérifier à partir de 15% du step (évite les faux positifs très tôt)

        // ?????????????????????????????????????????????????????????????????????????????
        // Runtime caches / état (NOMS CONSERVÉS)
        // ?????????????????????????????????????????????????????????????????????????????

        private Vector2 _held;                 // dernier input maintenu (x,y)
        private bool _isMoving = false;
        private float _readyAtTime = 0f;       // quand un nouveau step est autorisé

        public bool IsStepping => _isMoving;
        public Vector2Int FromCell { get; private set; }
        public Vector2Int ToCell { get; private set; }
        public float MoveProgress { get; private set; } // 0..1 pendant un step

        // Position logique sur la grille
        [SerializeField] private Vector2Int _gridPos;
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
        /// 
        public void Sync(HeroData _spawn)
        {
            spawn = _spawn;
        }


        public void Init()
        {

            moveInteraction = new ActorInteractionData(_actorType, ActorInteractionType.OnMove);
            bumpInteraction = new ActorInteractionData(_actorType, ActorInteractionType.OnBump);
            leaveInteraction = new ActorInteractionData(_actorType, ActorInteractionType.OnLeave);
            intentInteraction = new ActorInteractionData(_actorType, ActorInteractionType.OnIntent);


            _nmes = FindObjectsByType<NMEController>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            // Coord grille du spawn (ex. D8)
            _gridPos = spawn.spawnCell;

            // Centre monde de la case de spawn
            Vector3 spawnCenterWorld = CenterXZ(_gridPos);

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
            if (IsExitMove(_gridPos, targetCell, actorDirection))
            {

                StartCoroutine(StepTo(targetCell, isExitMove: true));
                return;
            }

            // 1) hors-grille -> bump
            if (!InsideBounds(targetCell, 8, 8))
            {
                StartCoroutine(Bump(intendedDir));
                return;
            }

            bool collided = NavigationEvents.QueryCollision(_gridPos, targetCell, actorDirection);
            if (collided)
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
            Vector3 worldEnd = CenterXZ(target);
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
                Debug.Log("Exited");
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
            Vector3 bumpVector = start + new Vector3(dir.x, 0f, dir.y).normalized * bumpDistance;

            bool walltouched = false;
            float lerpT = 0f;
            
            while (lerpT < bumpDuration)
            {
                // progression anim
                lerpT += Time.deltaTime;
                float ratio = lerpT / bumpDuration;

                if (!walltouched && ratio >= bumpEventPing)
                {
                    walltouched = true;
                    ActorEvents.NotifyActorMove(this, bumpInteraction);
                }

                Vector3 worldPosAtThisFrame = Vector3.LerpUnclamped(start, bumpVector, bumpLerpCurve.Evaluate(ratio));
                if (lerpT > bumpDuration) lerpT = bumpDuration;
                transform.position = worldPosAtThisFrame;   
                yield return null;
            }


            // Retour
            /*lerpT = 0f;
            while (lerpT < 1f)
            {
                lerpT += Time.deltaTime /
                if (lerpT > 1f) lerpT = 1f;
                transform.position = Vector3.Lerp(start + bumpVector, start, lerpT);
                yield return null;
            }*/

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
        private Vector3 EntryOffset(CardinalDirection dir) => DirToWorld(dir) * LevelGlobalSettings.cellSize;

        /// <summary>
        /// Step d’entrée depuis l’extérieur jusqu’à la cellule de spawn.
        /// </summary>
        private System.Collections.IEnumerator SpawnInFromEdge()
        {
            yield return StepTo(_gridPos);
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
        private bool IsExitMove(Vector2Int from, Vector2Int to, CardinalDirection dir)
        {
            return NavigationEvents.QueryExitPortal(from, to, dir);
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

            // 3) (la GridGateSystem & TimedDoorSystem vont réinjecter leurs verrous juste après leur propre Reset)
            _gridPos = spawn.spawnCell;
            var outside = GridUtils.CenterXZ(_gridPos) + EntryOffset(spawn.spawnDirection);
            transform.position = outside;

            _isMoving = true;
            StartCoroutine(SpawnInFromEdge());
        }

    }
}
