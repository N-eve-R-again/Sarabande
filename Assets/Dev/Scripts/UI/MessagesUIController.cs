// FILE: Assets/Dev/Scripts/UI/MessagesUIController.cs
//
// Rôle
// - Met à jour l’icône et le compteur TMP "messages collectés / total".
// - À attacher sur l’objet UI_MessagesRoot dans la scène.
//
// Invariants
// - Aucun renommage de champs sérialisés ni de méthodes publiques.
// - Zéro changement de logique (juste des commentaires et une mise en forme lisible).

using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Sarabande.UI
{
    /// <summary>
    /// Contrôle minimal de l'UI messages : icône + compteur "collected/total".
    /// </summary>
    public class MessagesUIController : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private Image icon;       // Image "Icon" dans l'UI
        [SerializeField] private TMP_Text counter; // TextMeshPro "CounterTMP"

        /// <summary>
        /// Remplace l'icône affichée dans l'UI (null => inchangé).
        /// </summary>
        public void SetIcon(Sprite s)
        {
            if (icon) icon.sprite = s;
        }

        /// <summary>
        /// Met à jour le compteur "messages collectés / total".
        /// </summary>
        public void SetCounter(int collected, int total)
        {
            if (counter) counter.text = $"{collected}/{total}";
        }
    }
}

