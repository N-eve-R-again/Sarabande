using System.Collections.Generic;
using UnityEngine;
using Sarabande.Levels;
using Sarabande.Core;
using Sarabande.Player;
using UnityEngine.Events;

namespace Sarabande.Doors
{
    public class LeverSystem : MonoBehaviour, IResettable
    {
        [Header("Data")]
        [SerializeField] private LevelData levelData;
        [SerializeField] private TimedDoorSystem timedDoorSystem; // référence au composant ci-dessus
        [SerializeField, Min(0.001f)] private float cellSize = 1f;
        [SerializeField, Min(0.1f)] private float wallHeight = 1f;

        [Header("Visuals")]
        [SerializeField] private Material leverBaseMaterial;
        [SerializeField] private Material leverHandleMaterial;

        [Header("Wall anchoring")]
        [SerializeField] private float wallInset = 0.0f;                     // peut être négatif pour rentrer dans le mur
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

        private HeroController _hero;
        private Transform _parent;

        private class LeverRuntime
        {
            public Vector2Int cell;
            public EdgeDirection requireFacing;
            public int doorIndex;
            public bool isOn; // OFF par défaut
            public Transform handle; // pour une petite rotation visuelle
            public bool pressedLatch; // true tant que le joueur maintient la poussée
            public Transform pivot; // pivot de rotation
        }

        private readonly List<LeverRuntime> _levers = new();

        private void Awake()
        {
            if (!levelData || !timedDoorSystem) { Debug.LogError("[LeverSystem] Références manquantes."); enabled = false; return; }

            _hero = FindObjectOfType<HeroController>(true);

            // S’ABONNER ICI (côté levier)
            timedDoorSystem.DoorClosed += OnDoorClosed;

            _parent = new GameObject("Levers").transform;
            _parent.SetParent(transform, false);

            Build();
        }

        private void Build()
        {
            _levers.Clear();
            if (levelData.levers == null) return;
            if (levelData.timedDoors == null || levelData.timedDoors.Count == 0)
            {
                Debug.LogWarning("[LeverSystem] Aucun TimedDoor dans LevelData, les leviers seront ignorés.");
                return;
            }

            int idx = 0;
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

                // 3.1) Position ancrée au mur demandé (requireFacing)
                Vector3 c = GridCenter(cell);
                float half = cellSize * 0.5f;

                // Position au bord de la case, selon le mur ciblé
                Vector3 basePos = c;
                switch (r.requireFacing)
                {
                    case EdgeDirection.North: basePos.z = c.z + half - wallInset; break;
                    case EdgeDirection.South: basePos.z = c.z - half + wallInset; break;
                    case EdgeDirection.East: basePos.x = c.x + half - wallInset; break;
                    case EdgeDirection.West: basePos.x = c.x - half + wallInset; break;
                }

                // Hauteur de montage sur le mur (en proportion de wallHeight)
                float mountY = Mathf.Lerp(0f, wallHeight, mountHeightNorm);

                // --- Base (cylindre “plaque”) ---
                var baseGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                baseGo.name = $"LeverBase_{idx}";
                baseGo.transform.SetParent(_parent, false);
                baseGo.transform.position = new Vector3(basePos.x, mountY, basePos.z);

                // On oriente la base pour que son +Z pointe vers L’INTÉRIEUR de la case
                baseGo.transform.rotation = Quaternion.Euler(0f, InteriorYaw(r.requireFacing), 0f);

                // Dimensions : cylindre fin + un peu large pour être visible top-down
                float baseR = baseRadiusScale * cellSize;          // rayon (X/Z)
                float baseThick = baseThicknessScale * wallHeight; // épaisseur (Y)
                baseGo.transform.localScale = new Vector3(baseR, baseThick * 0.5f, baseR); // (Y est la moitié de la hauteur Unity=2)

                var colB = baseGo.GetComponent<Collider>(); if (colB) Destroy(colB);
                var mrB = baseGo.GetComponent<MeshRenderer>();
                if (mrB) { mrB.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mrB.receiveShadows = false; if (leverBaseMaterial) mrB.sharedMaterial = leverBaseMaterial; }

                // --- Pivot (empty) au centre de la base ---
                // La rotation se fera sur ce pivot, pas sur le cube directement
                var pivotGo = new GameObject($"LeverPivot_{idx}").transform;
                pivotGo.SetParent(baseGo.transform, false);
                // 0.5f = rayon local du cylindre (Unity). On ajoute un tout petit débord pour être bien “à l’extérieur”.
                const float worldOutset = 0.005f; // 5 mm monde
                float localOutset = (baseGo.transform.lossyScale.z > 0f) ? (worldOutset / baseGo.transform.lossyScale.z) : 0f;
                pivotGo.localPosition = new Vector3(0f, 0f, 0.5f + localOutset);


                // --- Handle (cube) ---
                // Épaisseur (X/Y) et longueur (Z) qui sort dans la case
                float hT = handleThicknessScale * cellSize;
                float hL = handleLengthScale * cellSize;

                var handleGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                handleGo.name = $"LeverHandle_{idx}";
                handleGo.transform.SetParent(pivotGo, false);

                // Place la poignée pour que son extrémité “haute” coïncide avec le pivot (elle sort sur +Z)
                handleGo.transform.localScale = new Vector3(hT, hT, hL);
                handleGo.transform.localPosition = new Vector3(0f, 0f, hL * 0.5f);

                var colH = handleGo.GetComponent<Collider>(); if (colH) Destroy(colH);
                var mrH = handleGo.GetComponent<MeshRenderer>();
                if (mrH) { mrH.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mrH.receiveShadows = false; if (leverHandleMaterial) mrH.sharedMaterial = leverHandleMaterial; }

                // stocke les refs
                r.handle = handleGo.transform;
                r.pivot = pivotGo;

                SetHandleVisual(r, false); // poignée vers le bas au spawn

                idx++;
            }
        }

        private static float InteriorYaw(EdgeDirection d) => d switch
        {
            EdgeDirection.North => 180f, // intérieur = Sud
            EdgeDirection.East => 270f, // intérieur = Ouest
            EdgeDirection.South => 0f,   // intérieur = Nord
            EdgeDirection.West => 90f,  // intérieur = Est
            _ => 0f
        };

        private Vector3 GridCenter(Vector2Int c)
            => new Vector3((c.x + 0.5f) * cellSize, 0f, (c.y + 0.5f) * cellSize);

        private Vector2Int DirToVec(EdgeDirection d) => d switch
        {
            EdgeDirection.North => Vector2Int.up,
            EdgeDirection.East => Vector2Int.right,
            EdgeDirection.South => Vector2Int.down,
            EdgeDirection.West => Vector2Int.left,
            _ => Vector2Int.zero
        };

        private void Update()
        {
            if (!_hero) return;

            foreach (var lv in _levers)
            {
                // HÉRO doit être SUR la case
                bool onCell = _hero && _hero.GridPos == lv.cell;
                // et pousser dans la direction requise (intention courante)
                Vector2Int intent = _hero ? _hero.CurrentIntentDir : Vector2Int.zero;
                bool pressedNow = onCell && (intent == DirToVec(lv.requireFacing));

                // --- FRONT MONTANT ---
                // on lit l'ancien état AVANT de l'écraser
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

        private void SetHandleVisual(LeverRuntime lv, bool on)
        {
            if (!lv.pivot) return;
            // Angles POSITIFS dans l’Inspector
            // OFF = poignée vers le bas  => -handleAngleDown
            // ON  = poignée vers le haut => +handleAngleUp
            float angle = on ? -handleAngleUp : handleAngleDown;

            // Rotation autour de X local du pivot (la poignée sort sur +Z local)
            lv.pivot.localRotation = Quaternion.Euler(angle, 0f, 0f);
        }


        // Reset = tout OFF
        public void ResetToInitial()
        {
            foreach (var lv in _levers)
            {
                lv.isOn = false;
                lv.pressedLatch = false;  // on “relâche” la latche
                SetHandleVisual(lv, false);
            }
        }
        private void OnDestroy()
        {
            if (timedDoorSystem != null)
                timedDoorSystem.DoorClosed -= OnDoorClosed;
        }
        private void OnDoorClosed(int doorIndex)
        {
            foreach (var lv in _levers)
            {
                if (lv.doorIndex == doorIndex)
                {
                    lv.isOn = false;
                    lv.pressedLatch = false;     // on “relâche” la latche
                    SetHandleVisual(lv, false);  // poignée visuelle en OFF
                }
            }
        }
    }
}
