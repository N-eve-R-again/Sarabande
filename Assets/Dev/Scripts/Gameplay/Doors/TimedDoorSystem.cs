// FILE: Assets/Dev/Scripts/Doors/TimedDoorSystem.cs
//
// Rôle (résumé)
// - Gère des portes “temporisées” bloquant une CELLULE entière (à la différence des grid gates qui bloquent une ARÊTE).
// - À l’ouverture (via levier/trigger), la porte devient traversable pendant N secondes, avec SFX d’ouverture + “tick” régulier.
// - Tant qu’un acteur (Héros/NME) occupe la cellule, ou est en train d’y entrer suffisamment (seuil), la fermeture est différée.
// - À la fermeture, la cellule redevient bloquée (Héros/NME) et le visuel “bouchon” est affiché, plus SFX de fermeture.
// - Implémente IResettable : remet toutes les portes à l’état fermé.
//
// Invariants (ne pas casser)
// - AUCUN renommage de champs sérialisés, événements, méthodes publiques ou signatures.
// - Logique identique à l’originale (mêmes conditions, mêmes seuils, mêmes appels aux autres systèmes).
// - Les changements se limitent aux commentaires, à la mise en forme, et à des noms de variables **locales** plus clairs.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Sarabande.Levels;
using Sarabande.Core;
using Sarabande.Player;
using Sarabande.NME;
using System;
using static Sarabande.Core.GridUtils;

namespace Sarabande.Doors
{
    public class TimedDoorSystem : MonoBehaviour, IResettable
    {
        // ?????????????????????????????????????????????????????????????????????????????
        // Serialized fields (noms conservés)
        // ?????????????????????????????????????????????????????????????????????????????

        [Header("Data")]
        [SerializeField, Min(0.001f)] private float cellSize = 1f;
        [SerializeField, Min(0.1f)] private float wallHeight = 1f;
        [SerializeField] private bool useLevelContext = true;
        [SerializeField] private Sarabande.Core.LevelContext levelContext;
        [SerializeField, HideInInspector] private Sarabande.Levels.LevelData levelData;

        [Header("Visuals")]
        [SerializeField] private Material doorMaterial;         // visuel “bouchon” quand FERMÉ
        [SerializeField] private string obstaclesLayerName = "Obstacles"; // pour bloquer la LOS NME

        [Header("Hold rule")]
        [SerializeField, Range(0f, 1f)] private float enterHoldThreshold = 0.25f; // ?25% d'entrée => on maintient ouvert

        [Header("Audio")]
        [SerializeField] private AudioClip wooshOpenClip;
        [SerializeField] private AudioClip wooshCloseClip;
        [SerializeField] private AudioClip tickClip;
        [SerializeField, Range(0f, 1f)] private float sfxVolume = 0.9f;
        [SerializeField, Range(0f, 1f)] private float tickVolume = 0.6f;
        [SerializeField, Range(0f, 1f)] private float spatialBlend = 1f; // 3D
        [SerializeField] private float minDistance = 2f;
        [SerializeField] private float maxDistance = 20f;
        [SerializeField, Min(0f)] private float tickStartDelay = 0.08f; // petit délai pour laisser respirer le woosh d’ouverture
        [SerializeField] private float tickIntervalSeconds = -1f; // <= 0 => on prend automatiquement tickClip.length

        // ?????????????????????????????????????????????????????????????????????????????
        // Acteurs (runtime)
        // ?????????????????????????????????????????????????????????????????????????????

        // refs acteurs (pour la règle "ne pas écraser")
        private HeroController _hero;
        private NMEController[] _nmes;

        // Notifications (inchangées)
        public event Action<int> DoorOpened;
        public event Action<int> DoorClosed;

        // ?????????????????????????????????????????????????????????????????????????????
        // Runtime state
        // ?????????????????????????????????????????????????????????????????????????????

        // état porte
        private enum DoorState { Closed, Open, OpeningTimer }

        private class DoorRuntime
        {
            public int index;
            public Vector2Int cell;
            public float baseOpenSeconds;
            public DoorState state;
            public GameObject blockerGO;
            public Coroutine timerCo;

            // Tick “planifié” uniquement (plus d’AudioSource par porte)
            public Coroutine tickCo;
        }

        private readonly List<DoorRuntime> _doors = new();
        private Transform _parent;

        // ?????????????????????????????????????????????????????????????????????????????
        // Unity lifecycle
        // ?????????????????????????????????????????????????????????????????????????????

        /// <summary>
        /// Vérifie le LevelData, récupère Héros/NME, crée un parent hiérarchique et construit toutes les portes.
        /// </summary>
        private void Awake()
        {
            if (!levelData) { Debug.LogError("[TimedDoorSystem] LevelData manquant."); enabled = false; return; }

            _hero = FindFirstObjectByType<HeroController>(FindObjectsInactive.Include);
            _nmes = FindObjectsByType<NMEController>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            _parent = new GameObject("TimedDoors").transform;
            _parent.SetParent(transform, false);

            Build();
        }

        // ?????????????????????????????????????????????????????????????????????????????
        // Build & visuals
        // ?????????????????????????????????????????????????????????????????????????????

        /// <summary>
        /// Construit toutes les portes à partir du LevelData et applique l’état initial (FERMÉ).
        /// </summary>
        private void Build()
        {
            _doors.Clear();
            if (levelData.timedDoors == null) return;

            foreach (var spec in levelData.timedDoors)
            {
                var cell = new Vector2Int(spec.cell.x, spec.cell.z);

                // Visuel “bouchon fermé”
                var blockerGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
                blockerGO.name = $"Door_{cell.x}_{cell.y}";
                blockerGO.transform.SetParent(_parent, false);

                Vector3 cellCenterWorld = Center(cell, cellSize);
                blockerGO.transform.position = new Vector3(cellCenterWorld.x, wallHeight * 0.5f, cellCenterWorld.z);
                blockerGO.transform.localScale = new Vector3(cellSize, wallHeight, cellSize);

                // matériau, layer, collider
                var meshRenderer = blockerGO.GetComponent<MeshRenderer>();
                if (meshRenderer)
                {
                    meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    meshRenderer.receiveShadows = false;
                    if (doorMaterial) meshRenderer.sharedMaterial = doorMaterial;
                }
                int obstaclesLayer = LayerMask.NameToLayer(obstaclesLayerName);
                if (obstaclesLayer != -1) blockerGO.layer = obstaclesLayer;

                var doorRuntime = new DoorRuntime
                {
                    index = _doors.Count,
                    cell = cell,
                    baseOpenSeconds = Mathf.Max(0.1f, spec.openSeconds),
                    state = DoorState.Closed,
                    blockerGO = blockerGO,
                    timerCo = null,
                    tickCo = null
                };
                _doors.Add(doorRuntime);

                SetBlockerVisible(doorRuntime, true);   // affiche le "bouchon" + layer Obstacles
                AddDynamicBlock(doorRuntime.cell);      // BLOQUE la case côté Hero/NME
            }
        }

        // ?????????????????????????????????????????????????????????????????????????????
        // API publique (appelée par les leviers/routers)
        // ?????????????????????????????????????????????????????????????????????????????

        /// <summary>
        /// Bascule l’état d’une porte par index.
        /// - Closed ? Open (avec timer)
        /// - Open / OpeningTimer ? fermeture immédiate
        /// </summary>
        public void ToggleDoor(int doorIndex)
        {
            if (doorIndex < 0 || doorIndex >= _doors.Count) return;
            var d = _doors[doorIndex];
            switch (d.state)
            {
                case DoorState.Closed:
                    OpenDoor(doorIndex, d.baseOpenSeconds);
                    break;
                case DoorState.Open:
                case DoorState.OpeningTimer:
                    // Fermeture immédiate
                    ForceClose(doorIndex);
                    break;
            }
        }

        /// <summary>
        /// Ouvre la porte pendant <paramref name="seconds"/> secondes.
        /// - Si déjà en timer: refresh du timer + tick (sans rejouer le woosh).
        /// - Si déjà ouverte sans timer: relance un timer (sans woosh).
        /// - Si fermée: ouvre (cache visuel, retire blocage), joue woosh, planifie ticks.
        /// </summary>
        public void OpenDoor(int doorIndex, float seconds)
        {
            if (doorIndex < 0 || doorIndex >= _doors.Count) return;
            var d = _doors[doorIndex];

            // Déjà en timer ? refresh timer + tic-tac
            if (d.state == DoorState.OpeningTimer && d.timerCo != null)
            {
                StopCoroutine(d.timerCo);
                d.timerCo = StartCoroutine(OpenTimerRoutine(d, seconds));
                StartTickFor(d, seconds);
                return;
            }
            // Déjà ouvert sans timer ? passe en OpeningTimer (sans woosh, mais avec tick)
            if (d.state == DoorState.Open)
            {
                d.timerCo = StartCoroutine(OpenTimerRoutine(d, seconds));
                d.state = DoorState.OpeningTimer;
                StartTickFor(d, seconds);
                return;
            }

            // Fermée ? OUVERT
            d.state = DoorState.OpeningTimer;
            SetBlockerVisible(d, false);
            RemoveDynamicBlock(d.cell);  // autorise le passage

            // AUDIO
            PlayWooshOpen(d);
            StartTickFor(d, seconds);

            d.timerCo = StartCoroutine(OpenTimerRoutine(d, seconds));
            DoorOpened?.Invoke(d.index);
        }

        /// <summary>
        /// Force la fermeture immédiate (coupe timer + ticks) puis tente de refermer
        /// ou de différer la fermeture si un acteur occupe/entre dans la cellule.
        /// </summary>
        public void ForceClose(int doorIndex)
        {
            if (doorIndex < 0 || doorIndex >= _doors.Count) return;
            var d = _doors[doorIndex];
            if (d.timerCo != null) { StopCoroutine(d.timerCo); d.timerCo = null; }
            StopTickFor(d); // coupe le tic-tac planifié

            TryCloseOrDefer(d);
        }

        // ?????????????????????????????????????????????????????????????????????????????
        // Routines & logique d’ouverture/fermeture
        // ?????????????????????????????????????????????????????????????????????????????

        /// <summary>
        /// Timer d’ouverture avec “hold open” si un acteur occupe/entre suffisamment dans la cellule.
        /// </summary>
        private IEnumerator OpenTimerRoutine(DoorRuntime d, float seconds)
        {
            float remainingSeconds = Mathf.Max(0.01f, seconds);
            while (remainingSeconds > 0f)
            {
                remainingSeconds -= Time.deltaTime;

                // Si quelqu’un est “dans” la case (ou en train d’y entrer ? seuil), on prolonge implicitement
                if (ShouldHoldOpen(d.cell))
                    remainingSeconds += Time.deltaTime; // repousse d'autant

                yield return null;
            }
            d.timerCo = null;
            TryCloseOrDefer(d);
        }

        /// <summary>
        /// Tente de fermer la porte, sinon décale la fermeture jusqu’à libération de la cellule.
        /// </summary>
        private void TryCloseOrDefer(DoorRuntime d)
        {
            // règle: ne jamais refermer si HÉRO/NME occupe la case ou l'entre >= seuil
            if (ShouldHoldOpen(d.cell))
            {
                // on réessaie dans un court instant
                d.state = DoorState.OpeningTimer;
                d.timerCo = StartCoroutine(DeferCloseRoutine(d));
                return;
            }

            // fermer
            SetBlockerVisible(d, true);
            AddDynamicBlock(d.cell);   // interdit de passer
            d.state = DoorState.Closed;
            PlayWooshClose(d);
            DoorClosed?.Invoke(d.index);
        }

        /// <summary>
        /// Attend que la cellule ne soit plus occupée/entrée, puis ferme la porte.
        /// </summary>
        private IEnumerator DeferCloseRoutine(DoorRuntime d)
        {
            while (ShouldHoldOpen(d.cell))
                yield return null;

            SetBlockerVisible(d, true);
            AddDynamicBlock(d.cell);
            d.state = DoorState.Closed;
            d.timerCo = null;
            PlayWooshClose(d);
            DoorClosed?.Invoke(d.index);
        }

        /// <summary>
        /// Règle “hold open” : garde la porte ouverte si un acteur occupe la cellule cible
        /// ou si son step d’entrée a atteint le seuil <see cref="enterHoldThreshold"/>.
        /// </summary>
        private bool ShouldHoldOpen(Vector2Int cell)
        {
            if (_hero && OccupiesOrEntering(_hero.IsStepping, _hero.FromCell, _hero.ToCell, _hero.MoveProgress, _hero.GridPos, cell))
                return true;

            if (_nmes != null)
            {
                foreach (var n in _nmes)
                {
                    if (!n) continue;
                    if (OccupiesOrEntering(n.IsStepping, n.FromCell, n.ToCell, n.MoveProgress, n.GridPos, cell))
                        return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Vrai si l’acteur (mobile ou immobile) occupe déjà la cellule,
        /// ou s’il est assez engagé dans son step d’entrée (? seuil).
        /// </summary>
        private bool OccupiesOrEntering(bool isStepping, Vector2Int from, Vector2Int to, float t, Vector2Int gridPos, Vector2Int cell)
        {
            if (!isStepping)
                return gridPos == cell;

            if (from == cell) return true; // quitte la case => on maintient ouvert
            if (to == cell && t >= enterHoldThreshold) return true; // entre suffisamment
            return false;
        }

        /// <summary>
        /// Affiche/masque le visuel et active/désactive le blocage de LOS via layer.
        /// </summary>
        private void SetBlockerVisible(DoorRuntime d, bool visible)
        {
            if (!d.blockerGO) return;

            // Affichage
            var rendererComponent = d.blockerGO.GetComponent<Renderer>();
            if (rendererComponent) rendererComponent.enabled = visible;

            // Collision/LOS (le collider existe car on a créé un cube primitif)
            var colliderComponent = d.blockerGO.GetComponent<Collider>();
            if (colliderComponent) colliderComponent.enabled = visible;

            // Layer pour la LOS des NME
            int obstaclesLayer = LayerMask.NameToLayer(obstaclesLayerName);
            if (visible && obstaclesLayer != -1)
                d.blockerGO.layer = obstaclesLayer;        // fermé -> bloque LOS
            else
                d.blockerGO.layer = LayerMask.NameToLayer("Default"); // ouvert -> ne bloque pas
        }

        // ?????????????????????????????????????????????????????????????????????????????
        // Mise à jour des sets de collisions Héros/NME
        // ?????????????????????????????????????????????????????????????????????????????

        private void AddDynamicBlock(Vector2Int cell)
        {
            if (_hero) _hero.AddDynamicBlockCell(cell);
            if (_nmes != null) foreach (var n in _nmes) if (n) n.AddDynamicBlockCell(cell);
        }

        private void RemoveDynamicBlock(Vector2Int cell)
        {
            if (_hero) _hero.RemoveDynamicBlockCell(cell);
            if (_nmes != null) foreach (var n in _nmes) if (n) n.RemoveDynamicBlockCell(cell);
        }

        // ?????????????????????????????????????????????????????????????????????????????
        // API pour spawn dynamique de NME
        // ?????????????????????????????????????????????????????????????????????????????

        /// <summary>Rafraîchit les caches d’acteurs (si des NME apparaissent/disparaissent).</summary>
        public void RefreshActorCaches()
        {
            _hero = FindFirstObjectByType<HeroController>(FindObjectsInactive.Include);
            _nmes = FindObjectsByType<NMEController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        }

        /// <summary>
        /// Applique les blocages de toutes les portes actuellement fermées à un NME fraîchement spawné.
        /// </summary>
        public void ReapplyBlocksTo(NMEController nme)
        {
            if (nme == null) return;
            foreach (var d in _doors)
            {
                if (d == null) continue;
                if (d.state == DoorState.Closed)
                    nme.AddDynamicBlockCell(d.cell);
            }
        }

        // ?????????????????????????????????????????????????????????????????????????????
        // Audio via AudioHub
        // ?????????????????????????????????????????????????????????????????????????????

        private void PlayWooshOpen(DoorRuntime d)
        {
            if (!wooshOpenClip) return;
            Vector3 sfxPos = Center(d.cell, cellSize) + Vector3.up * 0.05f;
            Sarabande.Audio.AudioHub.I?.PlaySFXAt(wooshOpenClip, sfxPos, sfxVolume, spatialBlend, minDistance, maxDistance);
        }

        private void PlayWooshClose(DoorRuntime d)
        {
            if (!wooshCloseClip) return;
            Vector3 sfxPos = Center(d.cell, cellSize) + Vector3.up * 0.05f;
            Sarabande.Audio.AudioHub.I?.PlaySFXAt(wooshCloseClip, sfxPos, sfxVolume, spatialBlend, minDistance, maxDistance);
        }

        /// <summary>
        /// Lance un “tick” périodique pendant la fenêtre d’ouverture (après un léger délai).
        /// </summary>
        private void StartTickFor(DoorRuntime d, float seconds)
        {
            if (d.tickCo != null) { StopCoroutine(d.tickCo); d.tickCo = null; }
            if (!tickClip || seconds <= 0f) return;

            d.tickCo = StartCoroutine(TickWindowRoutine(d, seconds));
        }

        /// <summary>
        /// Routine de tick : attend <see cref="tickStartDelay"/>, puis joue un SFX toutes les
        /// <see cref="tickIntervalSeconds"/> (ou longueur du clip si valeur ? 0) jusqu’à la fin de fenêtre.
        /// </summary>
        private IEnumerator TickWindowRoutine(DoorRuntime d, float seconds)
        {
            float initialDelay = Mathf.Max(0f, tickStartDelay);
            if (initialDelay > 0f) yield return new WaitForSeconds(initialDelay);

            float remaining = Mathf.Max(0f, seconds - initialDelay);
            if (remaining <= 0f) yield break;

            float interval = (tickIntervalSeconds > 0f) ? tickIntervalSeconds : Mathf.Max(0.01f, tickClip.length);
            Vector3 sfxPos = Center(d.cell, cellSize) + Vector3.up * 0.05f;

            while (remaining > 0f)
            {
                Sarabande.Audio.AudioHub.I?.PlaySFXAt(tickClip, sfxPos, tickVolume, spatialBlend, minDistance, maxDistance);
                yield return new WaitForSeconds(interval);
                remaining -= interval;
            }

            StopTickFor(d);
        }

        /// <summary>Arrête la coroutine de tick si elle est en cours.</summary>
        private void StopTickFor(DoorRuntime d)
        {
            if (d.tickCo != null) { StopCoroutine(d.tickCo); d.tickCo = null; }
        }

        // ?????????????????????????????????????????????????????????????????????????????
        // Reset (IResettable)
        // ?????????????????????????????????????????????????????????????????????????????

        /// <summary>
        /// Remet toutes les portes à l’état “fermée”, coupe timers et ticks, réapplique les blocages.
        /// </summary>
        public void ResetToInitial()
        {
            foreach (var d in _doors)
            {
                if (d.timerCo != null) { StopCoroutine(d.timerCo); d.timerCo = null; }
                StopTickFor(d);

                SetBlockerVisible(d, true);
                d.state = DoorState.Closed;
                AddDynamicBlock(d.cell);
            }
        }

        // ?????????????????????????????????????????????????????????????????????????????
        // LevelContext wiring (inchangé)
        // ?????????????????????????????????????????????????????????????????????????????

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

        private void DetachContext()
        {
            if (levelContext != null)
                levelContext.LevelDataChanged -= HandleContextLevelDataChanged;
        }

        private void HandleContextLevelDataChanged(Sarabande.Levels.LevelData ld)
        {
            if (levelData == ld) return;
            levelData = ld;
#if UNITY_EDITOR
            if (!Application.isPlaying)
                UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        private void OnEnable() { AttachContext(); }
        private void OnDisable() { DetachContext(); }
#if UNITY_EDITOR
        private void OnValidate() { if (!Application.isPlaying) AttachContext(); }
#endif
    }
}
