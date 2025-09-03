using System.Collections;
using UnityEngine;
using TMPro;

namespace Sarabande.UI
{
    /// <summary>
    /// Popup de message avec TMP + fade in/out.
    /// Appelle Show(text, seconds) pour afficher puis masquer.
    /// </summary>
    public class MessagePopupUI_TMP : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private TMP_Text textTMP;
        [SerializeField] private CanvasGroup group;

        [Header("Fx")]
        [SerializeField, Min(0.01f)] private float fadeIn = 0.12f;
        [SerializeField, Min(0.01f)] private float fadeOut = 0.18f;

        private Coroutine _showCo;

        private void Reset()
        {
            textTMP = GetComponentInChildren<TMP_Text>(true);
            group = GetComponent<CanvasGroup>();
        }

        public void Show(string msg, float seconds)
        {
            if (_showCo != null) StopCoroutine(_showCo);
            _showCo = StartCoroutine(ShowRoutine(msg, Mathf.Max(0.05f, seconds)));
        }

        private IEnumerator ShowRoutine(string msg, float seconds)
        {
            if (group == null) yield break;

            if (textTMP) textTMP.text = msg ?? string.Empty;

            // Fade in
            float t = 0f;
            while (t < fadeIn)
            {
                t += Time.deltaTime;
                group.alpha = Mathf.InverseLerp(0f, fadeIn, t);
                yield return null;
            }
            group.alpha = 1f;

            // Hold
            float hold = seconds;
            while (hold > 0f)
            {
                hold -= Time.deltaTime;
                yield return null;
            }

            // Fade out
            t = 0f;
            while (t < fadeOut)
            {
                t += Time.deltaTime;
                group.alpha = Mathf.InverseLerp(fadeOut, 0f, t);
                yield return null;
            }
            group.alpha = 0f;

            _showCo = null;
        }
    }
}
