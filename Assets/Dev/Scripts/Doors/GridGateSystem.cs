// FILE: Assets/Dev/Scripts/Doors/GridGateSystem.cs
//
// Rôle (résumé)
// - Gère les "grid gates" (barreaux sur une ARÊTE entre deux cases).
// - Construit un visuel minimal (cube mince) positionné au milieu de l’arête A-B.
// - Bloque/débloque logiquement le passage entre A et B pour le Héros et les NME
//   via Add/RemoveDynamicEdgeBlock(...) sur leurs contrôleurs.
// - Expose une API simple: OpenGate(index), CloseGate(index), OpenAll().
// - Implémente IResettable : réapplique l’état initial défini dans LevelData.
//
// Invariants (à respecter absolument)
// - AUCUN renommage de champs sérialisés, propriétés, méthodes publiques.
// - Logique identique à l’originale (mêmes conditions, mêmes appels).
// - Les améliorations se limitent à la doc, au rangement visuel et à des noms de **variables locales** plus parlants.
//
// Dépendances
// - LevelData.gridGates : liste des spécifications de gates (case, side, initiallyOpen).
// - HeroController / NMEController : Add/RemoveDynamicEdgeBlock pour bloquer l’arête.
// - GridUtils : Center, DirToVec, DirToWorld, NormalizeEdge, etc.

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
        [SerializeField] private Material gateMaterial;                 // quad/cube fin (look éditeur)
        [SerializeField] private string gateLayerName = "Default";      // IMPORTANT: NE PAS utiliser "Obstacles"

        // ?????????????????????????????????????????????????????????????????????????????
        // Runtime (noms conservés)
        // ?????????????????????????????????????????????????????????????????????????????

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

        // ?????????????????????????????????????????????????????????????????????????????
        // Unity lifecycle
        // ?????????????????????????????????????????????????????????????????????????????

        /// <summary>
        /// Vérifie LevelData, récupère Hero/NME, crée un parent de hiérarchie et construit toutes les gates.
        /// </summary>
        private void Awake()
        {
            if (!levelData)
            {
                Debug.LogError("[GridGateSystem] LevelData manquant.");
                enabled = false;
                return;
            }
            _hero = FindFirstObjectByType<HeroController>(FindObjectsInactive.Include);
            _nmes = FindObjectsByType<NMEController>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            _parent = new GameObject("GridGates").transform;
            _parent.SetParent(transform, false);

            BuildAll();
        }

        // ?????????????????????????????????????????????????????????????????????????????
        // Build & visuals
        // ?????????????????????????????????????????????????????????????????????????????

        /// <summary>
        /// (Re)construit l’ensemble des gates à partir du LevelData, et applique l’état initial.
        /// </summary>
        private void BuildAll()
        {
            // Nettoyage
            for (int i = _parent.childCount - 1; i >= 0; i--)
                Destroy(_parent.GetChild(i).gameObject);
            _gates.Clear();

            if (levelData.gridGates == null || levelData.gridGates.Count == 0) return;

            for (int i = 0; i < levelData.gridGates.Count; i++)
            {
                var spec = levelData.gridGates[i];
                var a = new Vector2Int(spec.cell.x, spec.cell.z);
                var b = a + DirToVec(spec.side);

                // Visuel: mince "barre" posée AU MILIEU de l’arête A-B
                Vector3 edgeMidpointWorld = (Center(a, cellSize) + Center(b, cellSize)) * 0.5f;

                var gateGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
                gateGO.name = $"GridGate_{i}_({a.x},{a.y})-{spec.side}";
                gateGO.transform.SetParent(_parent, false);
                gateGO.transform.position = new Vector3(edgeMidpointWorld.x, wallHeight * 0.5f, edgeMidpointWorld.z);

                // Orientation + taille : plan vertical perpendiculaire à la direction de déplacement entre A et B
                // - si côté Nord/Sud : largeur sur X, faible épaisseur sur Z
                // - si côté Est/Ouest : largeur sur Z, faible épaisseur sur X
                float thickness = 0.04f * cellSize; // visuel fin
                if (spec.side == EdgeDirection.North || spec.side == EdgeDirection.South)
                {
                    gateGO.transform.localScale = new Vector3(cellSize, wallHeight, thickness);
                    gateGO.transform.rotation = Quaternion.identity;
                }
                else // Est/Ouest
                {
                    gateGO.transform.localScale = new Vector3(thickness, wallHeight, cellSize);
                    gateGO.transform.rotation = Quaternion.identity;
                }

                // Matériau + layer (NE PAS mettre sur "Obstacles")
                var meshRenderer = gateGO.GetComponent<MeshRenderer>();
                if (meshRenderer)
                {
                    meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    meshRenderer.receiveShadows = false;
                    if (gateMaterial) meshRenderer.sharedMaterial = gateMaterial;
                }

                // Pas de collider physique : la logique de blocage passe par la grille
                var colliderComponent = gateGO.GetComponent<Collider>();
                if (colliderComponent) Destroy(colliderComponent);

                int gateLayer = LayerMask.NameToLayer(gateLayerName);
                if (gateLayer != -1) gateGO.layer = gateLayer;

                // Runtime gate
                var gateRuntime = new GateRuntime
                {
                    index = i,
                    a = a,
                    b = b,
                    side = spec.side,
                    isOpen = spec.initiallyOpen,
                    go = gateGO
                };
                _gates.Add(gateRuntime);

                // État initial : si FERMÉ ? bloquer l’arête (Héros & NME) + montrer le visuel
                if (!gateRuntime.isOpen)
                {
                    AddEdgeBlock(gateRuntime.a, gateRuntime.b);
                    SetGateVisible(gateRuntime, true);
                }
                else
                {
                    SetGateVisible(gateRuntime, false);
                }
            }
        }

        /// <summary>
        /// Active/désactive le GameObject du visuel de gate.
        /// </summary>
        private void SetGateVisible(GateRuntime g, bool visible)
        {
            if (g.go) g.go.SetActive(visible);
        }

        // ?????????????????????????????????????????????????????????????????????????????
        // Public API
        // ?????????????????????????????????????????????????????????????????????????????

        /// <summary>
        /// Ouvre la gate par index : débloque l’arête et cache le visuel. Idempotent.
        /// </summary>
        public void OpenGate(int index)
        {
            if (index < 0 || index >= _gates.Count) return;
            var g = _gates[index];
            if (g.isOpen) return;

            g.isOpen = true;
            RemoveEdgeBlock(g.a, g.b);
            SetGateVisible(g, false);
        }

        /// <summary>
        /// Ferme la gate par index : bloque l’arête et montre le visuel. Idempotent.
        /// </summary>
        public void CloseGate(int index) // pas utilisé dans ce level, mais pratique
        {
            if (index < 0 || index >= _gates.Count) return;
            var g = _gates[index];
            if (!g.isOpen) return;

            g.isOpen = false;
            AddEdgeBlock(g.a, g.b);
            SetGateVisible(g, true);
        }

        /// <summary>
        /// Ouvre toutes les gates.
        /// </summary>
        public void OpenAll()
        {
            for (int i = 0; i < _gates.Count; i++) OpenGate(i);
        }

        /// <summary>
        /// Réapplique les arêtes fermées au NME donné (utile si un NME est reset isolément).
        /// </summary>
        public void ReapplyBlocksTo(Sarabande.NME.NMEController nme)
        {
            if (nme == null) return;
            for (int i = 0; i < _gates.Count; i++)
            {
                var g = _gates[i];
                if (!g.isOpen)
                {
                    // On ne touche qu’à ce NME (sans affecter Héros ou d’autres NME)
                    nme.AddDynamicEdgeBlock(g.a, g.b);
                }
            }
        }

        // ?????????????????????????????????????????????????????????????????????????????
        // Reset (IResettable)
        // ?????????????????????????????????????????????????????????????????????????????

        /// <summary>
        /// Réapplique l’état initial (initiallyOpen) depuis le LevelData sur toutes les gates.
        /// </summary>
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

        // ?????????????????????????????????????????????????????????????????????????????
        // Hooks HÉROS / NME (blocage d’arête logique)
        // ?????????????????????????????????????????????????????????????????????????????

        /// <summary>Ajoute un blocage d’arête (A-B) au Héros et à tous les NME.</summary>
        private void AddEdgeBlock(Vector2Int a, Vector2Int b)
        {
            if (_hero) _hero.AddDynamicEdgeBlock(a, b);
            if (_nmes != null) foreach (var n in _nmes) if (n) n.AddDynamicEdgeBlock(a, b);
        }

        /// <summary>Retire un blocage d’arête (A-B) au Héros et à tous les NME.</summary>
        private void RemoveEdgeBlock(Vector2Int a, Vector2Int b)
        {
            if (_hero) _hero.RemoveDynamicEdgeBlock(a, b);
            if (_nmes != null) foreach (var n in _nmes) if (n) n.RemoveDynamicEdgeBlock(a, b);
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
        /// Callback de mise à jour LevelData (éditeur + jeu). Ne rebuild pas automatiquement.
        /// </summary>
        private void HandleContextLevelDataChanged(Sarabande.Levels.LevelData ld)
        {
            if (levelData == ld) return;
            levelData = ld;

#if UNITY_EDITOR
            if (!Application.isPlaying)
                UnityEditor.EditorUtility.SetDirty(this); // l’Inspector reflète la mise à jour
#endif
            // NOTE: si tu souhaites reconstruire les gates lors d’un changement de LevelData,
            // appelle ici BuildAll(); (comportement inchangé par défaut).
        }

        private void OnEnable() { AttachContext(); }
        private void OnDisable() { DetachContext(); }
#if UNITY_EDITOR
        private void OnValidate() { if (!Application.isPlaying) AttachContext(); }
#endif
    }
}
