using System.Collections.Generic;
using System.Collections;
using UnityEngine;

namespace Sarabande.Core
{
    public class ResetManager : MonoBehaviour
    {
        [SerializeField] private bool findOnAwake = true;
        private readonly List<IResettable> _resettables = new();

        [SerializeField] private Sarabande.UI.RewindOverlay rewindOverlay;
        [SerializeField, Min(0f)] private float rewindDuration = 1f;

        [Header("Audio")]
        [Tooltip("SFX joué quand le REWIND s’affiche.")]
        [SerializeField] private AudioClip rewindClip;
        [SerializeField, Range(0f, 1f)] private float rewindVolume = 1f;
        [Tooltip("0 = 2D (UI), 1 = 3D")]
        [SerializeField, Range(0f, 1f)] private float rewindSpatialBlend = 0f;

        public bool IsResetInProgress { get; private set; }

        private void Awake()
        {
            if (findOnAwake) CollectResettables();
        }

        public void CollectResettables()
        {
            _resettables.Clear();
            var all = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var mb in all) if (mb is IResettable r) _resettables.Add(r);
        }

        private static int ResetOrder(IResettable r)
        {
            if (r is Sarabande.Player.HeroController) return 0;
            if (r is Sarabande.NME.NMEController) return 0;
            if (r is Sarabande.Gates.GridGateSystem) return 1;
            if (r is Sarabande.Doors.TimedDoorSystem) return 1;
            return 2;
        }

        public void ResetAll()
        {
            CollectResettables();
            _resettables.Sort((a, b) => ResetOrder(a).CompareTo(ResetOrder(b)));
            foreach (var r in _resettables) r.ResetToInitial();
        }

        public void ResetWithRewind()
        {
            if (IsResetInProgress) return;
            StartCoroutine(ResetWithRewindRoutine());
        }

        private IEnumerator ResetWithRewindRoutine()
        {
            IsResetInProgress = true;

            // Affiche l’overlay + joue le SFX en même temps
            if (rewindOverlay) rewindOverlay.ShowFor(rewindDuration);
            PlayRewindSfx();

            yield return new WaitForSeconds(rewindDuration);
            ResetAll();
            IsResetInProgress = false;
        }

        [ContextMenu("Reset Level Now")]
        private void ContextResetNow() => ResetAll();

        private void PlayRewindSfx()
        {
            if (!rewindClip) return;
            var go = new GameObject("SFX_Rewind_OneShot");
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = false;
            src.clip = rewindClip;
            src.volume = rewindVolume;
            src.spatialBlend = rewindSpatialBlend; // par défaut 2D (collé à l’UI)
            src.Play();
            Destroy(go, rewindClip.length + 0.1f);
        }
    }
}
