using System.Collections.Generic;
using UnityEngine;
using Sarabande.Core;
using Sarabande.Levels;
using Sarabande.Player;   // HeroController
using Sarabande.NME;      // NMEController
using static Sarabande.Core.GridUtils;

namespace Sarabande.Gates
{
    public class GridGateSystem : MonoBehaviour, IResettable
    {
        [Header("Data")]
        [SerializeField, Min(0.001f)] private float cellSize = 1f;
        [SerializeField, Min(0.1f)] private float wallHeight = 1f;
        [SerializeField] private bool useLevelContext = true;
        [SerializeField] private Sarabande.Core.LevelContext levelContext;
        [SerializeField, HideInInspector] private Sarabande.Levels.LevelData levelData;


        [Header("Visuals")]
        [SerializeField] private Material gateMaterial;       // quad/cube fin (editor look)
        [SerializeField] private string gateLayerName = "Default"; // IMPORTANT: NE DOIT PAS être sur "Obstacles"

        private class GateRuntime
        {
            public int index;
            public Vector2Int a;            // case A
            public Vector2Int b;            // case B = A + dir
            public EdgeDirection side;      // côté de A
            public bool isOpen;             // état courant
            public GameObject go;           // visuel
        }

        private readonly List<GateRuntime> _gates = new();
        private Transform _parent;

        private HeroController _hero;
        private NMEController[] _nmes;

        private void Awake()
        {
            if (!levelData)
            {
                Debug.LogError("[GridGateSystem] LevelData manquant."); enabled = false; return;
            }
            _hero = FindFirstObjectByType<HeroController>(FindObjectsInactive.Include);
            _nmes = FindObjectsByType<NMEController>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            _parent = new GameObject("GridGates").transform;
            _parent.SetParent(transform, false);

            BuildAll();
        }

        private void BuildAll()
        {
            // clean
            for (int i = _parent.childCount - 1; i >= 0; i--) Destroy(_parent.GetChild(i).gameObject);
            _gates.Clear();

            if (levelData.gridGates == null || levelData.gridGates.Count == 0) return;

            for (int i = 0; i < levelData.gridGates.Count; i++)
            {
                var spec = levelData.gridGates[i];
                var a = new Vector2Int(spec.cell.x, spec.cell.z);
                var b = a + DirToVec(spec.side);

                // visuel: mince “barre/quad” placé AU MILIEU de l’arête A-B
                var mid = (Center(a, cellSize) + Center(b, cellSize)) * 0.5f;
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = $"GridGate_{i}_({a.x},{a.y})-{spec.side}";
                go.transform.SetParent(_parent, false);
                go.transform.position = new Vector3(mid.x, wallHeight * 0.5f, mid.z);

                // orientation+taille: plan vertical perpendiculaire au déplacement
                // - si côté Nord/Sud ? “épaisseur” sur Z, largeur sur X
                // - si côté Est/Ouest ? “épaisseur” sur X, largeur sur Z
                float thickness = 0.04f * cellSize;      // fin
                if (spec.side == EdgeDirection.North || spec.side == EdgeDirection.South)
                {
                    go.transform.localScale = new Vector3(cellSize, wallHeight, thickness);
                    go.transform.rotation = Quaternion.identity;
                }
                else // Est/Ouest
                {
                    go.transform.localScale = new Vector3(thickness, wallHeight, cellSize);
                    go.transform.rotation = Quaternion.identity;
                }

                // matériau + layer (NE PAS mettre sur "Obstacles")
                var mr = go.GetComponent<MeshRenderer>();
                if (mr)
                {
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    mr.receiveShadows = false;
                    if (gateMaterial) mr.sharedMaterial = gateMaterial;
                }
                var col = go.GetComponent<Collider>(); if (col) Destroy(col); // pas de collider nécessaire (logique grille)
                int gateLayer = LayerMask.NameToLayer(gateLayerName);
                if (gateLayer != -1) go.layer = gateLayer;

                var rt = new GateRuntime
                {
                    index = i,
                    a = a,
                    b = b,
                    side = spec.side,
                    isOpen = spec.initiallyOpen,
                    go = go
                };
                _gates.Add(rt);

                // état initial: si FERMÉ ? bloquer l'arête pour HÉRO & NME + montrer le visuel
                if (!rt.isOpen)
                {
                    AddEdgeBlock(rt.a, rt.b);
                    SetGateVisible(rt, true);
                }
                else
                {
                    SetGateVisible(rt, false);
                }
            }
        }

        private void SetGateVisible(GateRuntime g, bool visible)
        {
            if (g.go) g.go.SetActive(visible);
        }

        // --- API ---
        public void OpenGate(int index)
        {
            if (index < 0 || index >= _gates.Count) return;
            var g = _gates[index];
            if (g.isOpen) return;

            g.isOpen = true;
            RemoveEdgeBlock(g.a, g.b);
            SetGateVisible(g, false);
        }

        public void CloseGate(int index) // pas utilisé dans ce level, mais pratique
        {
            if (index < 0 || index >= _gates.Count) return;
            var g = _gates[index];
            if (!g.isOpen) return;

            g.isOpen = false;
            AddEdgeBlock(g.a, g.b);
            SetGateVisible(g, true);
        }

        public void OpenAll()
        {
            for (int i = 0; i < _gates.Count; i++) OpenGate(i);
        }

        // --- Reset (F5 / échec) ---
        public void ResetToInitial()
        {
            for (int i = 0; i < _gates.Count; i++)
            {
                var spec = levelData.gridGates[i];
                var g = _gates[i];
                g.isOpen = spec.initiallyOpen;
                if (g.isOpen)
                {
                    RemoveEdgeBlock(g.a, g.b);
                    SetGateVisible(g, false);
                }
                else
                {
                    AddEdgeBlock(g.a, g.b);
                    SetGateVisible(g, true);
                }
            }
        }

        // --- Hook HÉRO/NME (blocage d’arête logique) ---
        private void AddEdgeBlock(Vector2Int a, Vector2Int b)
        {
            if (_hero) _hero.AddDynamicEdgeBlock(a, b);
            if (_nmes != null) foreach (var n in _nmes) if (n) n.AddDynamicEdgeBlock(a, b);
        }
        private void RemoveEdgeBlock(Vector2Int a, Vector2Int b)
        {
            if (_hero) _hero.RemoveDynamicEdgeBlock(a, b);
            if (_nmes != null) foreach (var n in _nmes) if (n) n.RemoveDynamicEdgeBlock(a, b);
        }
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
                UnityEditor.EditorUtility.SetDirty(this); // l’inspector reflète la maj auto
#endif
            // NOTE: si ce système a besoin de se "rebuild" quand le LevelData change,
            // appelle ici ta méthode interne (ex: RebuildFromLevelData()).
        }
        private void OnEnable() { AttachContext(); }
        private void OnDisable() { DetachContext(); }
#if UNITY_EDITOR
        private void OnValidate() { if (!Application.isPlaying) AttachContext(); }
#endif
    }
}
