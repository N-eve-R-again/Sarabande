using System.Collections.Generic;
using UnityEngine;
using Sarabande.Levels;   // LevelContext, LevelData
using Sarabande.Core;     // GridUtils
using static Sarabande.Core.GridUtils;

namespace Sarabande.NME
{
    /// <summary>
    /// Instancie 0..N ennemis d'après LevelData.nmeSpawns.
    /// - Reconstruit à chaque changement de LevelData (LevelContext.LevelDataChanged).
    /// - Ne fait rien au Reset: chaque NMEController gère son ResetToInitial.
    /// </summary>
    public class NMESpawnSystem : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private LevelContext levelContext;
        [SerializeField] private GameObject nmePrefab;
        [SerializeField, Min(0.001f)] private float cellSize = 1f;

        [Header("Parent runtime (optionnel)")]
        [Tooltip("Contiendra les instances runtime des NME. Si vide, sera créé automatiquement.")]
        [SerializeField] private Transform nmeParent;

        private LevelData _levelData;
        private readonly List<NMEController> _spawned = new();

        public static event System.Action AfterRebuild;

        private void Awake()
        {
            if (!levelContext) levelContext = GetComponentInParent<LevelContext>();
        }

        private void OnEnable()
        {
            if (!levelContext)
            {
                Debug.LogWarning($"[{nameof(NMESpawnSystem)}] Aucun LevelContext parent.");
                return;
            }

            levelContext.LevelDataChanged += OnLevelDataChanged;
            OnLevelDataChanged(levelContext.LevelData); // init
        }

        private void OnDisable()
        {
            if (levelContext)
                levelContext.LevelDataChanged -= OnLevelDataChanged;

            ClearSpawned();
        }

        private void OnLevelDataChanged(LevelData ld)
        {
            _levelData = ld;
            if (_levelData == null || nmePrefab == null)
            {
                ClearSpawned();
                return;
            }
            Rebuild();
        }

        private void Rebuild()
        {
            EnsureParent();
            ClearSpawned();

            int count = _levelData.nmeSpawns != null ? _levelData.nmeSpawns.Count : 0;

            for (int i = 0; i < count; i++)
            {
                var gc = _levelData.nmeSpawns[i];
                var cell = new Vector2Int(gc.x, gc.z);

                var go = Instantiate(nmePrefab, nmeParent);
                go.name = $"NME_{i}_{gc.x}_{gc.z}";

                var nme = go.GetComponent<NMEController>();
                if (!nme)
                {
                    Debug.LogError("[NMESpawnSystem] Prefab NME sans NMEController.");
                    Destroy(go);
                    continue;
                }

                // Donne sa cellule de spawn au contrôleur (utilisée dans Start/Reset)
                nme.SetSpawnCell(cell);

                // Position monde immédiate pour éviter un flash à (0,0,0)
                go.transform.position = Center(cell, cellSize);

                if (_levelData.nmeFacings != null && i < _levelData.nmeFacings.Count)
                {
                    nme.OverrideInitialFacing(_levelData.nmeFacings[i]);
                }

                _spawned.Add(nme);
            }

            // --- RECOLLAGE DES SYSTÈMES DYNAMIQUES ---

            // 1) Grilles/portes fines : re-pousser les arêtes fermées vers chaque NME
            var gates = FindFirstObjectByType<Sarabande.Gates.GridGateSystem>(FindObjectsInactive.Include);
            if (gates)
            {
                foreach (var n in _spawned) if (n) gates.ReapplyBlocksTo(n);
            }

            // 2) Timed Doors : rafraîchir caches (héros+NME) puis re-pousser toutes les cases des portes fermées
            var doors = FindFirstObjectByType<Sarabande.Doors.TimedDoorSystem>(FindObjectsInactive.Include);
            if (doors)
            {
                doors.RefreshActorCaches();
                foreach (var n in _spawned) if (n) doors.ReapplyBlocksTo(n);
            }

            // (les PressurePads se refresheront via l’évènement AfterRebuild

            //notifier tous les sytèmes dépendants
            AfterRebuild?.Invoke();
        }

        private void ClearSpawned()
        {
            // Détruit les NME instanciés
            for (int i = _spawned.Count - 1; i >= 0; i--)
                if (_spawned[i]) Destroy(_spawned[i].gameObject);
            _spawned.Clear();

            // Et tout enfant restant sous nmeParent, au cas où
            if (nmeParent)
            {
                for (int i = nmeParent.childCount - 1; i >= 0; i--)
                    Destroy(nmeParent.GetChild(i).gameObject);
            }
        }

        private void EnsureParent()
        {
            if (!nmeParent)
            {
                var go = new GameObject("NME_Runtime");
                nmeParent = go.transform;
                nmeParent.SetParent(transform, false); // transform == LevelRoot
            }
        }
    }
}
