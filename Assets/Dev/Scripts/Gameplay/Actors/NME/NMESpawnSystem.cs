// FILE: Assets/Dev/Scripts/NME/NMESpawnSystem.cs
//
// Rôle (résumé)
// - Instancie 0..N ennemis depuis LevelData.nmeSpawns.
// - Se reconstruit à chaque changement de LevelData (LevelContext.LevelDataChanged).
// - Ne gère pas le Reset global : chaque NMEController possède son propre ResetToInitial.
// - Après (re)build, “recolle” les systèmes dynamiques (GridGateSystem, TimedDoorSystem) et notifie AfterRebuild.
//
// Invariants
// - AUCUN renommage de champs sérialisés, méthodes publiques, signatures, ni de l’événement static AfterRebuild.
// - Logique identique à l’originale. Uniquement commentaires et renommage de variables LOCALES.

using System.Collections.Generic;
using UnityEngine;
using Sarabande.Levels;   // LevelContext, LevelData
using Sarabande.Core;     // GridUtils
using static Sarabande.Core.GridUtils;

namespace Sarabande.NME
{
    /// <summary>
    /// Instancie les NME décrits dans <see cref="LevelData.nmeSpawns"/> et se met à jour si le LevelData change.
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

        // --- runtime ---
        private LevelData _levelData;
        private readonly List<NMEController> _spawned = new();

        /// <summary>
        /// Notifié après chaque Rebuild pour permettre aux systèmes dépendants de se rafraîchir (pads, etc.).
        /// </summary>
        public static event System.Action AfterRebuild;

        // ?????????????????????????????????????????????????????????????????????????????
        // Unity lifecycle
        // ?????????????????????????????????????????????????????????????????????????????

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

            ClearSpawned(); // nettoyage si le système s’éteint
        }

        // ?????????????????????????????????????????????????????????????????????????????
        // LevelData ? rebuild
        // ?????????????????????????????????????????????????????????????????????????????

        /// <summary>
        /// Callback de LevelContext : met à jour la source et reconstruit si possible.
        /// </summary>
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

        /// <summary>
        /// Recrée toutes les instances NME à partir du LevelData courant.
        /// </summary>
        private void Rebuild()
        {
            return;
            EnsureParent();
            ClearSpawned();

            int count = (_levelData.nmeSpawns != null) ? _levelData.nmeSpawns.Count : 0;

            for (int i = 0; i < count; i++)
            {
                var gc = _levelData.nmeSpawns[i];
                var cell = new Vector2Int(gc.x, gc.z);

                // Instanciation + naming
                var nmeGO = Instantiate(nmePrefab, nmeParent);
                nmeGO.name = $"NME_{i}_{gc.x}_{gc.z}";

                // Contrôleur
                var nme = nmeGO.GetComponent<NMEController>();
                if (!nme)
                {
                    Debug.LogError("[NMESpawnSystem] Prefab NME sans NMEController.");
                    Destroy(nmeGO);
                    continue;
                }

                // Cellule de spawn (utilisée par le NME dans Start/Reset)
                nme.SetSpawnCell(cell);

                // Position monde immédiate (évite un flash à (0,0,0))
                nmeGO.transform.position = Center(cell, cellSize);

                // Facing optionnel (si présent dans LevelData)
                if (_levelData.nmeFacings != null && i < _levelData.nmeFacings.Count)
                    nme.OverrideInitialFacing(_levelData.nmeFacings[i]);

                _spawned.Add(nme);
            }

            // --- RECOLLAGE DES SYSTÈMES DYNAMIQUES ---
            // 1) Grilles fines : re-pousser les arêtes fermées vers chaque NME
            /*var gates = FindFirstObjectByType<Sarabande.Gates.GridGateSystem>(FindObjectsInactive.Include);
            if (gates != null)
                foreach (var n in _spawned) if (n) gates.ReapplyBlocksTo(n);
            */
            // 2) Timed Doors : refresh caches (héros+NME) puis re-pousser toutes les cases bloquées
            /*var doors = FindFirstObjectByType<Sarabande.Doors.TimedDoorSystem>(FindObjectsInactive.Include);
            if (doors != null)
            {
                doors.RefreshActorCaches();
                foreach (var n in _spawned) if (n) doors.ReapplyBlocksTo(n);
            }
            */
            // Les PressurePads écoutent AfterRebuild pour rafraîchir leurs caches
            AfterRebuild?.Invoke();
        }

        // ?????????????????????????????????????????????????????????????????????????????
        // Helpers
        // ?????????????????????????????????????????????????????????????????????????????

        /// <summary>
        /// Crée un parent “NME_Runtime” si absent (pour une hiérarchie propre).
        /// </summary>
        private void EnsureParent()
        {
            if (nmeParent != null) return;
            var p = new GameObject("NME_Runtime");
            p.transform.SetParent(transform, false);
            nmeParent = p.transform;
        }

        /// <summary>
        /// Détruit toutes les instances NME déjà présentes et vide la liste _spawned.
        /// </summary>
        private void ClearSpawned()
        {
            for (int i = _spawned.Count - 1; i >= 0; i--)
                if (_spawned[i]) Destroy(_spawned[i].gameObject);
            _spawned.Clear();
        }
    }
}
