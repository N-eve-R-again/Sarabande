using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Sarabande.UI
{
    /// <summary>
    /// Met à jour l'icône et le compteur (TextMeshPro) "messages collectés / total".
    /// À mettre sur UI_MessagesRoot.
    /// </summary>
    public class MessagesUIController : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private Image icon;       // ton Image "Icon"
        [SerializeField] private TMP_Text counter; // ton TextMeshPro "CounterTMP"

        public void SetIcon(Sprite s)
        {
            if (icon) icon.sprite = s;
        }

        public void SetCounter(int collected, int total)
        {
            if (counter) counter.text = $"{collected}/{total}";
        }
    }
}
