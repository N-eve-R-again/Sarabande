using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Sarabande.Levels;
using Sarabande.Core;
using Sarabande.Player;
using Sarabande.NME;
using System;

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
        }

        private readonly List<DoorRuntime> _doors = new();
        private Transform _parent;

        private void Awake()
        {
            if (!levelData) { Debug.LogError("[TimedDoorSystem] LevelData manquant."); enabled = false; return; }

            _hero = FindObjectOfType<HeroController>(true);
            _nmes = FindObjectsOfType<NMEController>(true);

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

                Vector3 c = GridCenter(cell);
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

                // état initial = FERMÉ (B5 est déjà dans nonWalkables)
                var r = new DoorRuntime
                {
                    index = _doors.Count,
                    cell = cell,
                    baseOpenSeconds = Mathf.Max(0.1f, spec.openSeconds),
                    state = DoorState.Closed,
                    blockerGO = go,
                    timerCo = null
                };
                _doors.Add(r);
            }
        }

        private Vector3 GridCenter(Vector2Int c)
            => new Vector3((c.x + 0.5f) * cellSize, 0f, (c.y + 0.5f) * cellSize);

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

            // déjà ouvert => refresh timer
            if (d.state == DoorState.OpeningTimer && d.timerCo != null)
            {
                StopCoroutine(d.timerCo);
                d.timerCo = StartCoroutine(OpenTimerRoutine(d, seconds));
                return;
            }
            if (d.state == DoorState.Open)
            {
                d.timerCo = StartCoroutine(OpenTimerRoutine(d, seconds));
                d.state = DoorState.OpeningTimer;
                return;
            }

            // passer FERMÉ -> OUVERT
            d.state = DoorState.OpeningTimer;
            SetBlockerVisible(d, false);
            RemoveDynamicBlock(d.cell);  // autorise le passage
            d.timerCo = StartCoroutine(OpenTimerRoutine(d, seconds));

            // NEW: prévenir qu’on vient d’ouvrir
            DoorOpened?.Invoke(d.index);
        }

        public void ForceClose(int doorIndex)
        {
            if (doorIndex < 0 || doorIndex >= _doors.Count) return;
            var d = _doors[doorIndex];
            if (d.timerCo != null) { StopCoroutine(d.timerCo); d.timerCo = null; }
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

        private void SetBlockerVisible(DoorRuntime d, bool visible)
        {
            if (d.blockerGO) d.blockerGO.SetActive(visible);
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

        // --- Reset ---
        public void ResetToInitial()
        {
            // re-fermer tout
            foreach (var d in _doors)
            {
                if (d.timerCo != null) { StopCoroutine(d.timerCo); d.timerCo = null; }
                SetBlockerVisible(d, true);
                d.state = DoorState.Closed;
                AddDynamicBlock(d.cell); // s'assure que la case redevient bloquée
            }
        }
    }
}
