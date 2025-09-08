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
            var all = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var mb in all)
                if (mb is IResettable r)
                    _resettables.Add(r);
        }

        private static int ResetOrder(IResettable r)
        {
            // 0 = Acteurs (vident d’abord leurs caches dynamiques)
            if (r is Sarabande.Player.HeroController) return 0;
            if (r is Sarabande.NME.NMEController) return 0;

            // 1 = Systèmes qui ré-appliquent des verrous/collisions
            if (r is Sarabande.Gates.GridGateSystem) return 1;
            if (r is Sarabande.Doors.TimedDoorSystem) return 1;

            // 2 = le reste
            return 2;
        }

        /// <summary>Réinitialise tous les objets enregistrés.</summary>
        public void ResetAll()
        {
            CollectResettables();
            _resettables.Sort((a, b) => ResetOrder(a).CompareTo(ResetOrder(b)));

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
