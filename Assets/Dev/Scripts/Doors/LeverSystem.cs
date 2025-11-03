// FILE: Assets/Dev/Scripts/Doors/LeverSystem.cs
//
// Rôle (résumé)
// - Construit des leviers ancrés sur un “mur” d’une cellule donnée (visuel simple base + poignée).
// - En jeu: si le Héros est sur la cellule et pousse dans la direction requise, le levier toggle ON/OFF.
// - ON ? ouvre la porte liée (TimedDoorSystem.OpenDoor) ; OFF ? forceClose de la porte.
// - Se resynchronise quand une porte se referme (événement DoorClosed) pour remettre la poignée en OFF.
// - Implémente IResettable : tout repasse en OFF.
//
// Invariants
// - AUCUN renommage de champs sérialisés, méthodes publiques, événements.
// - Logique identique à l’originale (mêmes conditions sur input/facing, mêmes appels TimedDoorSystem).
// - Les changements se limitent aux commentaires, ordre visuel et renommages **locaux** plus explicites.

using System.Collections.Generic;
using UnityEngine;
using Sarabande.Levels;
using Sarabande.Core;
using Sarabande.Player;
using UnityEngine.Events;
using static Sarabande.Core.GridUtils;

namespace Sarabande.Doors
{
    public class LeverSystem : MonoBehaviour, IResettable
    {
        // ?????????????????????????????????????????????????????????????????????????????
        // Serialized fields (noms conservés)
        // ?????????????????????????????????????????????????????????????????????????????

        [Header("Data")]
        [SerializeField] private TimedDoorSystem timedDoorSystem; // référence au composant ci-dessus
        [SerializeField, Min(0.001f)] private float cellSize = 1f;
        [SerializeField, Min(0.1f)] private float wallHeight = 1f;
        [SerializeField] private bool useLevelContext = true;
        [SerializeField] private Sarabande.Core.LevelContext levelContext;
        [SerializeField, HideInInspector] private Sarabande.Levels.LevelData levelData;

        [Header("Visuals")]
        [SerializeField] private Material leverBaseMaterial;
        [SerializeField] private Material leverHandleMaterial;

        [Header("Wall anchoring")]
        [SerializeField] private float wallInset = 0.0f;                      // peut être négatif pour rentrer dans le mur
        [SerializeField, Range(0f, 1f)] private float mountHeightNorm = 0.7f; // 0=sol, 1=haut du mur
        [SerializeField, Range(0.05f, 0.5f)] private float baseRadiusScale = 0.16f;   // rayon (X/Z) relatif à la case
        [SerializeField, Range(0.02f, 0.3f)] private float baseThicknessScale = 0.06f; // épaisseur (Y) relative à la hauteur de mur

        [Header("Handle")]
        [SerializeField, Range(0.02f, 0.25f)] private float handleThicknessScale = 0.08f; // épaisseur (X/Y) relative à la case
        [SerializeField, Range(0.2f, 1.2f)] private float handleLengthScale = 0.7f;       // longueur (Z) relative à la case
        [SerializeField, Range(0f, 90f)] private float handleAngleUp = 35f;               // angle poignée en position ON
        [SerializeField, Range(0f, 90f)] private float handleAngleDown = 45f;             // angle poignée en position OFF

        [Header("Events")]
        public UnityEvent onAnyLeverTurnedOn;
        public UnityEvent onAnyLeverTurnedOff;

        // ?????????????????????????????????????????????????????????????????????????????
        // Runtime
        // ?????????????????????????????????????????????????????????????????????????????

        private HeroController _hero;
        private Transform _parent;

        private class LeverRuntime
        {
            public Vector2Int cell;
            public EdgeDirection requireFacing;
            public int doorIndex;
            public bool isOn;               // OFF par défaut
            public Transform handle;        // pour une petite rotation visuelle
            public bool pressedLatch;       // true tant que le joueur maintient la poussée
            public Transform pivot;         // pivot de rotation
        }

        private readonly List<LeverRuntime> _levers = new();

        // ?????????????????????????????????????????????????????????????????????????????
        // Unity lifecycle
        // ?????????????????????????????????????????????????????????????????????????????

        /// <summary>
        /// Vérifie les références, s’abonne au TimedDoorSystem, construit les leviers.
        /// </summary>
        private void Awake()
        {
            if (!levelData || !timedDoorSystem)
            {
                Debug.LogError("[LeverSystem] Références manquantes.");
                enabled = false;
                return;
            }

            _hero = FindFirstObjectByType<HeroController>(FindObjectsInactive.Include);

            // S’ABONNER ICI (côté levier) pour se mettre en OFF quand la porte se referme.
            timedDoorSystem.DoorClosed += OnDoorClosed;

            _parent = new GameObject("Levers").transform;
            _parent.SetParent(transform, false);

            Build();
        }

        // ?????????????????????????????????????????????????????????????????????????????
        // Build (visuels)
        // ?????????????????????????????????????????????????????????????????????????????

        /// <summary>
        /// Construit les leviers à partir de LevelData (base cylindre + pivot + poignée cube).
        /// </summary>
        private void Build()
        {
            _levers.Clear();
            if (levelData.levers == null) return;

            if (levelData.timedDoors == null || levelData.timedDoors.Count == 0)
            {
                Debug.LogWarning("[LeverSystem] Aucun TimedDoor dans LevelData, les leviers seront ignorés.");
                return;
            }

            int leverIndex = 0;
            foreach (var spec in levelData.levers)
            {
                var cell = new Vector2Int(spec.cell.x, spec.cell.z);
                var r = new LeverRuntime
                {
                    cell = cell,
                    requireFacing = spec.requireFacing,
                    doorIndex = Mathf.Clamp(spec.linkedDoorIndex, 0, levelData.timedDoors.Count - 1),
                    isOn = false
                };
                _levers.Add(r);

                // 1) Position ancrée au mur demandé (requireFacing)
                Vector3 cellCenterWorld = Center(cell, cellSize);
                float halfCell = cellSize * 0.5f;

                // Position au bord de la case, selon le mur ciblé
                Vector3 baseWorldPos = cellCenterWorld;
                switch (r.requireFacing)
                {
                    case EdgeDirection.North: baseWorldPos.z = cellCenterWorld.z + halfCell - wallInset; break;
                    case EdgeDirection.South: baseWorldPos.z = cellCenterWorld.z - halfCell + wallInset; break;
                    case EdgeDirection.East: baseWorldPos.x = cellCenterWorld.x + halfCell - wallInset; break;
                    case EdgeDirection.West: baseWorldPos.x = cellCenterWorld.x - halfCell + wallInset; break;
                }

                // Hauteur de montage sur le mur (en proportion de wallHeight)
                float mountWorldY = Mathf.Lerp(0f, wallHeight, mountHeightNorm);

                // --- Base (cylindre “plaque”) ---
                var leverBaseGO = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                leverBaseGO.name = $"LeverBase_{leverIndex}";
                leverBaseGO.transform.SetParent(_parent, false);
                leverBaseGO.transform.position = new Vector3(baseWorldPos.x, mountWorldY, baseWorldPos.z);

                // On oriente la base pour que son +Z pointe vers l’INTÉRIEUR de la case
                leverBaseGO.transform.rotation = Quaternion.Euler(0f, InteriorYaw(r.requireFacing), 0f);

                // Dimensions : cylindre fin + large pour lisibilité top-down
                float baseRadiusWorld = baseRadiusScale * cellSize;          // rayon (X/Z)
                float baseThicknessWorld = baseThicknessScale * wallHeight;  // épaisseur (Y)
                leverBaseGO.transform.localScale = new Vector3(baseRadiusWorld, baseThicknessWorld * 0.5f, baseRadiusWorld); // (Y = moitié car Unity cylindre fait 2 en hauteur)

                var baseCollider = leverBaseGO.GetComponent<Collider>(); if (baseCollider) Destroy(baseCollider);
                var baseMeshRenderer = leverBaseGO.GetComponent<MeshRenderer>();
                if (baseMeshRenderer)
                {
                    baseMeshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    baseMeshRenderer.receiveShadows = false;
                    if (leverBaseMaterial) baseMeshRenderer.sharedMaterial = leverBaseMaterial;
                }

                // --- Pivot (empty) au centre de la base ---
                // La rotation se fera sur ce pivot, pas sur la poignée directement
                var pivotTransform = new GameObject($"LeverPivot_{leverIndex}").transform;
                pivotTransform.SetParent(leverBaseGO.transform, false);
                // 0.5f = rayon local du cylindre Unity. Ajout d’un léger débord vers l’intérieur de la case.
                const float worldOutset = 0.005f; // 5 mm monde
                float localOutset = (leverBaseGO.transform.lossyScale.z > 0f) ? (worldOutset / leverBaseGO.transform.lossyScale.z) : 0f;
                pivotTransform.localPosition = new Vector3(0f, 0f, 0.5f + localOutset);

                // --- Handle (cube) ---
                // Épaisseur (X/Y) et longueur (Z) qui sort dans la case
                float handleThicknessWorld = handleThicknessScale * cellSize;
                float handleLengthWorld = handleLengthScale * cellSize;

                var handleGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
                handleGO.name = $"LeverHandle_{leverIndex}";
                handleGO.transform.SetParent(pivotTransform, false);

                // Place la poignée pour que son extrémité “haute” coïncide avec le pivot (elle sort sur +Z local)
                handleGO.transform.localScale = new Vector3(handleThicknessWorld, handleThicknessWorld, handleLengthWorld);
                handleGO.transform.localPosition = new Vector3(0f, 0f, handleLengthWorld * 0.5f);

                var handleCollider = handleGO.GetComponent<Collider>(); if (handleCollider) Destroy(handleCollider);
                var handleMeshRenderer = handleGO.GetComponent<MeshRenderer>();
                if (handleMeshRenderer)
                {
                    handleMeshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    handleMeshRenderer.receiveShadows = false;
                    if (leverHandleMaterial) handleMeshRenderer.sharedMaterial = leverHandleMaterial;
                }

                // stocke les refs
                r.handle = handleGO.transform;
                r.pivot = pivotTransform;

                SetHandleVisual(r, false); // poignée vers le bas au spawn

                leverIndex++;
            }
        }

        /// <summary>
        /// Yaw (rotation Y) pour que le +Z local pointe vers l’intérieur de la case.
        /// </summary>
        private static float InteriorYaw(EdgeDirection d) => d switch
        {
            EdgeDirection.North => 180f, // intérieur = Sud
            EdgeDirection.East => 270f, // intérieur = Ouest
            EdgeDirection.South => 0f,   // intérieur = Nord
            EdgeDirection.West => 90f,  // intérieur = Est
            _ => 0f
        };

        // ?????????????????????????????????????????????????????????????????????????????
        // Gameplay loop
        // ?????????????????????????????????????????????????????????????????????????????

        /// <summary>
        /// Si le Héros est sur la cellule et pousse vers <c>requireFacing</c>, on toggle ON/OFF une seule fois par appui.
        /// </summary>
        private void Update()
        {
            if (!_hero) return;

            foreach (var lv in _levers)
            {
                // Héros doit être SUR la case
                bool heroIsOnCell = _hero && _hero.GridPos == lv.cell;
                // …et pousser dans la direction requise (intention courante)
                Vector2Int heroIntent = _hero ? _hero.CurrentIntentDir : Vector2Int.zero;
                bool pressedNow = heroIsOnCell && (heroIntent == DirToVec(lv.requireFacing));

                // FRONT MONTANT : on lit l'ancien état AVANT de l'écraser
                bool wasPressed = lv.pressedLatch;

                if (pressedNow && !wasPressed)
                {
                    // Toggle 1 seule fois à l’appui
                    if (!lv.isOn)
                    {
                        lv.isOn = true;
                        timedDoorSystem.OpenDoor(lv.doorIndex, levelData.timedDoors[lv.doorIndex].openSeconds);
                        SetHandleVisual(lv, true);

                        // bruit / event
                        Sarabande.Core.NoiseSystem.Emit(lv.cell);
                        onAnyLeverTurnedOn?.Invoke();
                    }
                    else
                    {
                        lv.isOn = false;
                        timedDoorSystem.ForceClose(lv.doorIndex);
                        SetHandleVisual(lv, false);

                        onAnyLeverTurnedOff?.Invoke();
                    }
                }

                // on MET À JOUR la latch APRÈS avoir testé le front
                lv.pressedLatch = pressedNow;
            }
        }

        /// <summary>
        /// Met à jour la rotation de la poignée selon l’état (ON/OFF).
        /// </summary>
        private void SetHandleVisual(LeverRuntime lv, bool on)
        {
            if (!lv.pivot) return;

            // Angles POSITIFS dans l’Inspector
            // OFF = poignée vers le bas  => -handleAngleDown
            // ON  = poignée vers le haut => +handleAngleUp
            float angle = on ? -handleAngleUp : handleAngleDown;

            // Rotation autour de X local du pivot (la poignée sort sur +Z)
            lv.pivot.localRotation = Quaternion.Euler(angle, 0f, 0f);
        }

        // ?????????????????????????????????????????????????????????????????????????????
        // Reset (IResettable)
        // ?????????????????????????????????????????????????????????????????????????????

        /// <summary>
        /// Tout OFF : désactive latch et remet la poignée vers le bas.
        /// </summary>
        public void ResetToInitial()
        {
            foreach (var lv in _levers)
            {
                lv.isOn = false;
                lv.pressedLatch = false;  // on “relâche” la latch
                SetHandleVisual(lv, false);
            }
        }

        // ?????????????????????????????????????????????????????????????????????????????
        // TimedDoor events wiring
        // ?????????????????????????????????????????????????????????????????????????????

        private void OnDestroy()
        {
            if (timedDoorSystem != null)
                timedDoorSystem.DoorClosed -= OnDoorClosed;
        }

        /// <summary>
        /// Quand la porte liée se referme, on repasse le levier visuellement et logiquement en OFF.
        /// </summary>
        private void OnDoorClosed(int doorIndex)
        {
            foreach (var lv in _levers)
            {
                if (lv.doorIndex == doorIndex)
                {
                    lv.isOn = false;
                    lv.pressedLatch = false;     // on “relâche” la latch
                    SetHandleVisual(lv, false);  // poignée visuelle en OFF
                }
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
