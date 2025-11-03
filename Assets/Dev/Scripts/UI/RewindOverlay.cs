// FILE: Assets/Dev/Scripts/UI/RewindOverlay.cs
//
// Rôle
// - Affiche brièvement un overlay (CanvasGroup) pendant le rewind.
// - Utilisé par ResetManager.
//
// Invariants
// - API identique, logique strictement inchangée.

using System.Collections;
using UnityEngine;

namespace Sarabande.UI
{
    /// <summary>
    /// Affiche un fondu court d'overlay via CanvasGroup pendant la durée demandée.
    /// </summary>
    public class RewindOverlay : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;

        private void Awake()
        {
            if (group)
            {
                group.alpha = 0f;
                group.interactable = false;
                group.blocksRaycasts = false;
            }
        }

        /// <summary>
        /// Affiche l'overlay pendant <paramref name="seconds"/> (fade instantané).
        /// </summary>
        public void ShowFor(float seconds)
        {
            StartCoroutine(ShowRoutine(seconds));
        }

        private IEnumerator ShowRoutine(float s)
        {
            if (group) group.alpha = 1f;
            yield return new WaitForSeconds(s);
            if (group) group.alpha = 0f;
        }
    }
}