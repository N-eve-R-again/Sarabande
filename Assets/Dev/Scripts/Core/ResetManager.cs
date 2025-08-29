using System.Collections.Generic;
using System.Collections;
using UnityEngine;

namespace Sarabande.Core
{
    /// <summary>
    /// Trouve tous les IResettable de la scène et peut les réinitialiser.
    /// À attacher sur LevelRoot.
    /// </summary>
    public class ResetManager : MonoBehaviour
    {
        [SerializeField] private bool findOnAwake = true;
        private readonly List<IResettable> _resettables = new();

        [SerializeField] private Sarabande.UI.RewindOverlay rewindOverlay;
        [SerializeField, Min(0f)] private float rewindDuration = 1f;

        public bool IsResetInProgress { get; private set; }

        private void Awake()
        {
            if (findOnAwake) CollectResettables();
        }

        /// <summary>Recherche tous les IResettable (même inactifs) et met à jour la liste.</summary>
        public void CollectResettables()
        {
            _resettables.Clear();
            var all = FindObjectsOfType<MonoBehaviour>(true);
            foreach (var mb in all)
                if (mb is IResettable r)
                    _resettables.Add(r);
        }

        /// <summary>Réinitialise tous les objets enregistrés.</summary>
        public void ResetAll()
        {
            CollectResettables();
            foreach (var r in _resettables)
                r.ResetToInitial();
        }
        public void ResetWithRewind()
        {
            if (IsResetInProgress) return;               // anti double-lancement
            StartCoroutine(ResetWithRewindRoutine());
        }

        private IEnumerator ResetWithRewindRoutine()
        {
            IsResetInProgress = true;
            if (rewindOverlay) rewindOverlay.ShowFor(rewindDuration);
            yield return new WaitForSeconds(rewindDuration);
            ResetAll();
            IsResetInProgress = false;
        }

        [ContextMenu("Reset Level Now")]
        private void ContextResetNow() => ResetAll();
    }
}
