// FILE: Assets/Dev/Scripts/Core/ResetListener.cs
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sarabande.Core
{
    /// <summary>
    /// Écoute l'action "Reset" du New Input System en mode "Send Messages"
    /// et déclenche un reset global avec rewind via ResetManager.
    /// À placer sur le même GameObject que le PlayerInput.
    /// </summary>
    public class ResetListener : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private ResetManager resetManager; // doit pointer vers l'instance unique de la scène

        /// <summary>
        /// Méthode appelée automatiquement par PlayerInput (Send Messages)
        /// lorsque l'action "Reset" est pressée.
        /// </summary>
        private void OnReset(InputValue input)
        {
            // Par sécurité, on vérifie la référence et l'état du bouton.
            if (resetManager == null) return;
            if (!input.isPressed) return;

            // Lance le rewind visuel puis ResetAll.
            resetManager.ResetWithRewind();
        }
    }
}

