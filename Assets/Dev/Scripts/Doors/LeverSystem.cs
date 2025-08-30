using System.Collections.Generic;
using UnityEngine;
using Sarabande.Levels;
using Sarabande.Core;
using Sarabande.Player;

namespace Sarabande.Doors
{
    public class LeverSystem : MonoBehaviour, IResettable
    {
        [Header("Data")]
        [SerializeField] private LevelData levelData;
        [SerializeField] private TimedDoorSystem timedDoorSystem; // référence au composant ci-dessus
        [SerializeField, Min(0.001f)] private float cellSize = 1f;
        [SerializeField, Min(0.1f)] private float leverHeight = 0.4f;

        [Header("Visuals")]
        [SerializeField] private Material leverBaseMaterial;
        [SerializeField] private Material leverHandleMaterial;

        [Header("Wall anchoring")]
        [SerializeField, Min(0.0f)] private float wallInset = 0.08f;              // écart au mur (évite de “mordre” dedans)
        [SerializeField, Range(0.05f, 0.5f)] private float baseRadiusScale = 0.12f; // rayon base cylindre relatif à la case
        [SerializeField, Range(0.2f, 1.0f)] private float handleLengthScale = 0.5f; // hauteur/longueur relative de la poignée

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

                // Visuel base + poignée
                Vector3 c = GridCenter(cell);
                float half = cellSize * 0.5f;

                // 3.1) Position ancrée au mur demandé (requireFacing)
                Vector3 basePos = c;
                switch (r.requireFacing)
                {
                    case EdgeDirection.North: basePos.z = c.z + half - wallInset; break;
                    case EdgeDirection.South: basePos.z = c.z - half + wallInset; break;
                    case EdgeDirection.East: basePos.x = c.x + half - wallInset; break;
                    case EdgeDirection.West: basePos.x = c.x - half + wallInset; break;
                }

                // 3.2) Instanciation de la base (cylindre), orientée vers le mur
                var baseGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                baseGo.name = $"LeverBase_{idx}";
                baseGo.transform.SetParent(_parent, false);
                baseGo.transform.position = new Vector3(basePos.x, leverHeight * 0.25f, basePos.z);
                baseGo.transform.rotation = Quaternion.Euler(0f, FacingYaw(r.requireFacing), 0f);

                // rayon de la base (x/z), hauteur ? 0.5 * leverHeight (comme avant)
                float baseR = baseRadiusScale * cellSize;
                baseGo.transform.localScale = new Vector3(baseR, leverHeight * 0.25f, baseR);

                var colB = baseGo.GetComponent<Collider>(); if (colB) Destroy(colB);
                var mrB = baseGo.GetComponent<MeshRenderer>();
                if (mrB) { mrB.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mrB.receiveShadows = false; if (leverBaseMaterial) mrB.sharedMaterial = leverBaseMaterial; }

                // 3.3) Poignée (cube) : on garde la même logique mais on ajuste sa taille
                var handleGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                handleGo.name = $"LeverHandle_{idx}";
                handleGo.transform.SetParent(baseGo.transform, false);

                // hauteur de la poignée relative (un peu plus longue si tu veux)
                float handleH = leverHeight * handleLengthScale;
                // épaisseur fine
                float handleT = cellSize * 0.1f;

                handleGo.transform.localPosition = new Vector3(0f, leverHeight * 0.5f, 0f);
                handleGo.transform.localScale = new Vector3(handleT, handleH, handleT);

                var colH = handleGo.GetComponent<Collider>(); if (colH) Destroy(colH);
                var mrH = handleGo.GetComponent<MeshRenderer>();
                if (mrH) { mrH.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mrH.receiveShadows = false; if (leverHandleMaterial) mrH.sharedMaterial = leverHandleMaterial; }

                r.handle = handleGo.transform;

                idx++;
            }
        }

        private static float FacingYaw(EdgeDirection d) => d switch
        {
            EdgeDirection.North => 0f,
            EdgeDirection.East => 90f,
            EdgeDirection.South => 180f,
            EdgeDirection.West => 270f,
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

                // EDGE: on ne toggle que sur front montant (pressed passer de false -> true)
                if (pressedNow && !lv.pressedLatch)
                {
                    if (!lv.isOn)
                    {
                        lv.isOn = true;
                        timedDoorSystem.OpenDoor(lv.doorIndex, levelData.timedDoors[lv.doorIndex].openSeconds);
                        SetHandleVisual(lv, true);
                    }
                    else
                    {
                        lv.isOn = false;
                        timedDoorSystem.ForceClose(lv.doorIndex); // fermeture immédiate (ta règle)
                        SetHandleVisual(lv, false);
                    }
                }

                // MAJ latch
                lv.pressedLatch = pressedNow;
            }

        }

        private static Vector2Int HeldToCardinal(HeroController hero)
        {
            // On re-calcul l'intention comme dans le HeroController (même deadzone)
            // Ici simplifié: on récupère l’orientation actuelle comme proxy si besoin,
            // mais l’idéal serait d’exposer une propriété CurrentIntentDir dans HeroController.
            // Pour rester simple, on prend la forward du hero.
            Vector3 f = hero.transform.forward;
            float ax = Mathf.Abs(f.x);
            float az = Mathf.Abs(f.z);
            if (ax > az) return f.x > 0 ? Vector2Int.right : Vector2Int.left;
            else return f.z > 0 ? Vector2Int.up : Vector2Int.down;
        }

        private void SetHandleVisual(LeverRuntime lv, bool on)
        {
            if (!lv.handle) return;
            // petit basculement visuel
            float angle = on ? 35f : -35f;
            lv.handle.localRotation = Quaternion.Euler(angle, 0f, 0f);
        }

        // Reset = tout OFF
        public void ResetToInitial()
        {
            foreach (var lv in _levers)
            {
                lv.isOn = false;
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
