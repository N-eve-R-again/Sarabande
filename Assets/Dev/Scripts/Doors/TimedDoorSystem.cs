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
        [Header("Data")]
        [SerializeField] private LevelData levelData;
        [SerializeField, Min(0.001f)] private float cellSize = 1f;
        [SerializeField, Min(0.1f)] private float wallHeight = 1f;

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
        [SerializeField] private float tickIntervalSeconds = -1f; // L’intervalle entre deux "tick". <= 0 => on prend automatiquement tickClip.length

        // refs acteurs (pour la règle "ne pas écraser")
        private HeroController _hero;
        private NMEController[] _nmes;

        public event Action<int> DoorOpened;
        public event Action<int> DoorClosed;

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

            // AUDIO
            public AudioSource sfx;      // pour les woosh (OneShot)
            public AudioSource tick;     // pour le tic-tac (Play/Stop)
            public Coroutine tickCo;     // pour chronométrer la durée de tick
        }

        private readonly List<DoorRuntime> _doors = new();
        private Transform _parent;

        private void Awake()
        {
            if (!levelData) { Debug.LogError("[TimedDoorSystem] LevelData manquant."); enabled = false; return; }

            _hero = FindFirstObjectByType<HeroController>(FindObjectsInactive.Include);
            _nmes = FindObjectsByType<NMEController>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            _parent = new GameObject("TimedDoors").transform;
            _parent.SetParent(transform, false);

            Build();
        }

        private void Build()
        {
            _doors.Clear();
            if (levelData.timedDoors == null) return;

            foreach (var spec in levelData.timedDoors)
            {
                var cell = new Vector2Int(spec.cell.x, spec.cell.z);

                // Visuel “bouchon fermé”
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = $"Door_{cell.x}_{cell.y}";
                go.transform.SetParent(_parent, false);

                Vector3 c = Center(cell, cellSize);
                go.transform.position = new Vector3(c.x, wallHeight * 0.5f, c.z);
                go.transform.localScale = new Vector3(cellSize, wallHeight, cellSize);

                // mat, layer, collider
                var mr = go.GetComponent<MeshRenderer>();
                if (mr)
                {
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    mr.receiveShadows = false;
                    if (doorMaterial) mr.sharedMaterial = doorMaterial;
                }
                int obsLayer = LayerMask.NameToLayer(obstaclesLayerName);
                if (obsLayer != -1) go.layer = obsLayer;

                // AUDIO par porte : deux sources sur le GO (même position que la porte)
                var sfx = go.AddComponent<AudioSource>();
                sfx.playOnAwake = false;
                sfx.loop = false;
                sfx.volume = sfxVolume;
                sfx.spatialBlend = spatialBlend;
                sfx.minDistance = minDistance;
                sfx.maxDistance = maxDistance;

                var tick = go.AddComponent<AudioSource>();
                tick.playOnAwake = false;
                tick.loop = false;               // eviter la loop continue qui fait buzzer
                tick.volume = tickVolume;
                tick.spatialBlend = spatialBlend;
                tick.minDistance = minDistance;
                tick.maxDistance = maxDistance;
                tick.clip = null;                 //on veut jouer en oneshot le tick, pour chaque tick

                // état initial = FERMÉ (B5 est déjà dans nonWalkables)
                var r = new DoorRuntime
                {
                    index = _doors.Count,
                    cell = cell,
                    baseOpenSeconds = Mathf.Max(0.1f, spec.openSeconds),
                    state = DoorState.Closed,
                    blockerGO = go,
                    timerCo = null,
                    sfx = sfx,
                    tick = tick,
                    tickCo = null
                };
                _doors.Add(r);

                SetBlockerVisible(r, true);   // affiche le "bouchon" + layer Obstacles
                AddDynamicBlock(r.cell);      // BLOQUE la case côté Hero/NME
            }
        }

        // --- API appelée par les leviers ---

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
                    // fermeture immédiate
                    ForceClose(doorIndex);
                    break;
            }
        }

        public void OpenDoor(int doorIndex, float seconds)
        {
            if (doorIndex < 0 || doorIndex >= _doors.Count) return;
            var d = _doors[doorIndex];

            // Déjà ouvert ET timer en cours ? refresh (uniquement le tick)
            if (d.state == DoorState.OpeningTimer && d.timerCo != null)
            {
                // refresh timer "logique"
                StopCoroutine(d.timerCo);
                d.timerCo = StartCoroutine(OpenTimerRoutine(d, seconds));

                // refresh fenêtre de tick "configurée"
                StartTickFor(d, seconds);
                return;
            }
            // Déjà ouvert (sans timer actif) ? repasse en OpeningTimer (sans woosh, mais tick)
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


        public void ForceClose(int doorIndex)
        {
            if (doorIndex < 0 || doorIndex >= _doors.Count) return;
            var d = _doors[doorIndex];
            if (d.timerCo != null) { StopCoroutine(d.timerCo); d.timerCo = null; }
            StopTickFor(d); // ? coupe le tic-tac tout de suite

            TryCloseOrDefer(d);
        }

        private IEnumerator OpenTimerRoutine(DoorRuntime d, float seconds)
        {
            float t = Mathf.Max(0.01f, seconds);
            while (t > 0f)
            {
                t -= Time.deltaTime;
                // si quelqu’un est “dans” la case (ou en train d’y entrer ? seuil), on prolonge implicitement
                if (ShouldHoldOpen(d.cell)) t += Time.deltaTime; // repousse d'autant
                yield return null;
            }
            d.timerCo = null;
            TryCloseOrDefer(d);
        }

        private void TryCloseOrDefer(DoorRuntime d)
        {
            // règle: ne jamais refermer si HÉRO/NME occupe la case ou l'entre >= 25%
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
            PlayWooshClose(d); // ? woosh au moment de la fermeture visuelle
            DoorClosed?.Invoke(d.index);
        }

        private IEnumerator DeferCloseRoutine(DoorRuntime d)
        {
            // petit polling
            while (ShouldHoldOpen(d.cell))
                yield return null;

            SetBlockerVisible(d, true);
            AddDynamicBlock(d.cell);
            d.state = DoorState.Closed;
            d.timerCo = null;
            PlayWooshClose(d); // ? woosh au moment de la fermeture visuelle
            DoorClosed?.Invoke(d.index);
        }

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

        private bool OccupiesOrEntering(bool isStepping, Vector2Int from, Vector2Int to, float t, Vector2Int gridPos, Vector2Int cell)
        {
            if (!isStepping)
                return gridPos == cell;

            if (from == cell) return true; // quitte la case => on maintient ouvert
            if (to == cell && t >= enterHoldThreshold) return true; // entre suffisamment
            return false;
        }

        private void SetBlockerVisible(DoorRuntime d, bool visible) //on a les sons sur le go de la porte, donc on le desactive pas mais on le rend invisible et non bloquant
        {
            if (!d.blockerGO) return;

            // Affichage
            var rend = d.blockerGO.GetComponent<Renderer>();
            if (rend) rend.enabled = visible;

            // Collision/LOS
            var col = d.blockerGO.GetComponent<Collider>();
            if (col) col.enabled = visible;

            // Layer pour la LOS des NME
            int obs = LayerMask.NameToLayer(obstaclesLayerName);
            if (visible && obs != -1)
                d.blockerGO.layer = obs;        // ferme -> bloque LOS
            else
                d.blockerGO.layer = LayerMask.NameToLayer("Default"); // ouvert -> ne bloque pas
        }


        // --- Mise à jour des sets de collisions Héro/NME ---
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
        // --- Sound ---
        private void PlayWooshOpen(DoorRuntime d)
        {
            if (d.sfx && wooshOpenClip) d.sfx.PlayOneShot(wooshOpenClip, sfxVolume);
        }
        private void PlayWooshClose(DoorRuntime d)
        {
            if (d.sfx && wooshCloseClip) d.sfx.PlayOneShot(wooshCloseClip, sfxVolume);
        }

        private void StartTickFor(DoorRuntime d, float seconds)
        {
            if (d.tickCo != null) { StopCoroutine(d.tickCo); d.tickCo = null; }
            if (!d.tick || !tickClip || seconds <= 0f) return;

            d.tickCo = StartCoroutine(TickWindowRoutine(d, seconds));
        }

        private IEnumerator TickWindowRoutine(DoorRuntime d, float seconds)
        {
            // 1) Laisse le woosh respirer un poil
            float delay = Mathf.Max(0f, tickStartDelay);
            if (delay > 0f) yield return new WaitForSeconds(delay);

            // 2) Temps restant pour le tic-tac "configuré"
            float remaining = Mathf.Max(0f, seconds - delay);
            if (remaining <= 0f) yield break;

            // 3) Intervalle réél des "tick"
            float interval = (tickIntervalSeconds > 0f) ? tickIntervalSeconds : Mathf.Max(0.01f, tickClip.length);

            while (remaining > 0f)
            {
                // Joue un "tick" discret (OneShot respecte spatialBlend, min/maxDistance, etc.)
                d.tick.PlayOneShot(tickClip, tickVolume);

                // Attendre la fin du tick (ou l’intervalle choisi)
                yield return new WaitForSeconds(interval);
                remaining -= interval;
            }

            // Fin de fenêtre ? on s’assure qu’aucun tick ne continue
            StopTickFor(d);
        }

        private void StopTickFor(DoorRuntime d)
        {
            if (d.tickCo != null) { StopCoroutine(d.tickCo); d.tickCo = null; }
            // Pas besoin de Stop() : on joue en OneShot, pas de boucle en cours.
        }

        // --- Reset ---
        public void ResetToInitial()
        {
            // re-fermer tout
            foreach (var d in _doors)
            {
                if (d.timerCo != null) { StopCoroutine(d.timerCo); d.timerCo = null; }
                StopTickFor(d); // ? coupe le tic-tac s’il jouait

                SetBlockerVisible(d, true);
                d.state = DoorState.Closed;
                AddDynamicBlock(d.cell);
            }
        }
    }
}
