using System.Collections;
using UnityEngine;

namespace Sarabande.UI
{
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
