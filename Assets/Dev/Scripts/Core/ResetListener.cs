using UnityEngine;
using UnityEngine.InputSystem;

namespace Sarabande.Core
{
    /// <summary>
    /// Écoute l'action "Reset" (F5) via PlayerInput (Send Messages) et appelle ResetManager.ResetAll().
    /// À mettre sur le même GameObject que le PlayerInput (ici: Hero), avec une référence vers ResetManager.
    /// </summary>
    public class ResetListener : MonoBehaviour
    {
        [SerializeField] private ResetManager resetManager;

        // PlayerInput en mode "Send Messages" appellera cette méthode
        private void OnReset(InputValue value)
        {
            if (value.isPressed && resetManager != null)
                resetManager.ResetWithRewind();
        }
    }
}
