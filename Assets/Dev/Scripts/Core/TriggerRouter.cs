// FILE: Assets/DEV/Scripts/Core/TriggerRouter.cs
using System.Collections.Generic;
using UnityEngine;
using Sarabande.Levels;
using Sarabande.Core;
using Sarabande.Doors;

namespace Sarabande.Triggers
{
    public class TriggerRouter : MonoBehaviour
    {
        [Header("Context")]
        [SerializeField] private bool useLevelContext = true;
        [SerializeField] private Sarabande.Core.LevelContext levelContext;
        [SerializeField, HideInInspector] private LevelData levelData;

        [Header("Targets")]
        [SerializeField] private TimedDoorSystem timedDoorSystem;

        private readonly Dictionary<string, List<LevelData.TriggerBinding>> _map = new();

        private void Awake()
        {
            if (!timedDoorSystem)
                timedDoorSystem = FindFirstObjectByType<TimedDoorSystem>(FindObjectsInactive.Include);
            Rebuild();
        }

        private void Rebuild()
        {
            _map.Clear();
            if (levelData == null || levelData.triggerBindings == null) return;

            foreach (var b in levelData.triggerBindings)
            {
                if (string.IsNullOrEmpty(b.id)) continue;
                if (!_map.TryGetValue(b.id, out var list))
                {
                    list = new List<LevelData.TriggerBinding>();
                    _map[b.id] = list;
                }
                list.Add(b);
            }
        }

        /// <summary>Déclenche toutes les actions liées à ce triggerId.</summary>
        public void Fire(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (!_map.TryGetValue(id, out var list)) return;

            foreach (var b in list)
            {
                switch (b.action)
                {
                    case LevelData.TriggerActionKind.TimedDoorOpen:
                        if (timedDoorSystem == null) continue;
                        int idx = Mathf.Max(0, b.targetIndex);
                        float secs = b.secondsOverride > 0f ? b.secondsOverride
                            : (levelData != null && levelData.timedDoors != null
                               && idx < levelData.timedDoors.Count
                               ? Mathf.Max(0.1f, levelData.timedDoors[idx].openSeconds)
                               : 1f);
                        timedDoorSystem.OpenDoor(idx, secs);
                        break;

                        // (plus tard) autres actions ici
                }
            }
        }

        /// <summary>Retourne la liste des index de portes ouvertes par ce triggerId.</summary>
        public List<int> GetTimedDoorTargetsForId(string id)
        {
            var result = new List<int>();
            if (string.IsNullOrEmpty(id)) return result;
            if (!_map.TryGetValue(id, out var list)) return result;

            foreach (var b in list)
            {
                if (b.action == LevelData.TriggerActionKind.TimedDoorOpen && b.targetIndex >= 0)
                    result.Add(b.targetIndex);
            }
            return result;
        }

        // --- LevelContext plumbing ---
        private void AttachContext()
        {
            if (!useLevelContext) return;
            if (!levelContext)
                levelContext = GetComponentInParent<Sarabande.Core.LevelContext>();

            if (levelContext != null)
            {
                levelContext.LevelDataChanged += HandleContextLevelDataChanged;
                HandleContextLevelDataChanged(levelContext.LevelData);
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
        private void HandleContextLevelDataChanged(LevelData ld)
        {
            if (levelData == ld) return;
            levelData = ld;
#if UNITY_EDITOR
            if (!Application.isPlaying)
                UnityEditor.EditorUtility.SetDirty(this);
#endif
            Rebuild();
        }
        private void OnEnable() { AttachContext(); }
        private void OnDisable() { DetachContext(); }
#if UNITY_EDITOR
        private void OnValidate() { if (!Application.isPlaying) AttachContext(); }
#endif
    }
}
