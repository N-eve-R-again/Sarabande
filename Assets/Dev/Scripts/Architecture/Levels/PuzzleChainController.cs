using System.Collections;
using UnityEngine;
using Sarabande.Levels;   // pour LevelContext, LevelData, PuzzleList (ton nouveau SO)
using Sarabande.Core;     // pour HeroController, ResetManager (adapte si besoin)

namespace Sarabande.Levels
{
    /// <summary>
    /// Enchaîne les puzzles d'un cercle dans une même scène :
    /// - Succès déclenché UNIQUEMENT par HeroController.onExit
    /// - Fade to black (musique non coupée), pas d'UI/SFX de rewind pendant la transition
    /// - SetLevelData(next) + ResetAll pour rebuild propre
    /// - Fin de démo après le dernier level de la liste
    /// </summary>
    public class PuzzleChainController : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private LevelContext levelContext;
        [SerializeField] private ResetManager resetManager;
        [SerializeField] private PuzzleList puzzleList;

        [Header("UI Fade (noir)")]
        [SerializeField] private CanvasGroup fadeCanvas;      // Panel noir plein écran (alpha 0 au démarrage)
        [SerializeField, Min(0f)] private float fadeDuration = 0.35f;
        [SerializeField, Min(0f)] private float blackHold = 0.10f;

        [Header("UI Rewind (optionnel)")]
        [Tooltip("Parent/Root de l'UI rewind à masquer pendant la transition inter-puzzles.")]
        [SerializeField] private GameObject rewindUIRoot;

        // NOTE messages : tu veux que les messages soient 'par puzzle'.
        // Si ton MessageSystem ne se vide pas au ResetAll, on ajoutera une petite méthode ClearAll() côté MessageSystem
        // et on la déclenchera depuis ce contrôleur (voir fin de ce message).
        [Header("Messages (optionnel)")]
        [SerializeField] private MonoBehaviour messageSystem; // assigner l'instance si tu veux déclencher un ClearAll() dessus via SendMessage

        private int currentIndex = 0;
        private bool isTransitioning = false;

        private void Awake()
        {
            if (!levelContext) levelContext = FindFirstObjectByType<LevelContext>();
            if (!resetManager) resetManager = FindFirstObjectByType<ResetManager>();

            // Sélectionne l'index courant à partir du LevelData en cours (si présent dans la liste)
            if (puzzleList != null && levelContext != null && levelContext.LevelData != null)
            {
                int idx = puzzleList.levels.IndexOf(levelContext.LevelData);
                currentIndex = Mathf.Max(0, idx);
            }

            if (fadeCanvas) fadeCanvas.alpha = 0f; // démarre transparent
        }

        public void OnHeroExit()
        {
            if (!isActiveAndEnabled) return;
            if (isTransitioning) return;
            StartCoroutine(LoadNextPuzzleCoroutine());
        }

        private IEnumerator LoadNextPuzzleCoroutine()
        {
            isTransitioning = true;

            // 1) Fade to black (la musique continue : ne pas toucher au MusicSystem)
            if (fadeCanvas) yield return StartCoroutine(FadeRoutine(1f, fadeDuration));
            if (blackHold > 0f) yield return new WaitForSeconds(blackHold);

            // 2) Masquer l'UI rewind le temps de la transition (pas d'effet rewind entre puzzles)
            bool prevRewindActive = false;
            if (rewindUIRoot)
            {
                prevRewindActive = rewindUIRoot.activeSelf;
                rewindUIRoot.SetActive(false);
            }

            // 3) Messages : si tu as branché un messageSystem et ajouté une méthode ClearAll(),
            // on la déclenche ici via SendMessage (évite toute dépendance forte).
            if (messageSystem)
            {
                // Appelle ClearAll() si présent ; sinon, ne fait rien.
                messageSystem.SendMessage("ClearAll", SendMessageOptions.DontRequireReceiver);
            }

            // 4) Charger le prochain level et reset propre
            currentIndex++;
            bool hasNext = puzzleList != null
                           && puzzleList.levels != null
                           && currentIndex < puzzleList.levels.Count;

            if (hasNext)
            {
                var nextLevel = puzzleList.levels[currentIndex];

                // IMPORTANT : la musique continue. Assure-toi que MusicSystem n'est pas détruit/reset ici.
                levelContext.LevelData = nextLevel; // rebuild se basera sur ce LevelData

                // Reset global 'propre' (les systèmes existants gèrent déjà leur reset)
                resetManager.ResetAll();
            }
            else
            {
                // Fin de la séquence (pour l’instant, 3 puzzles)
                Debug.Log("[PuzzleChain] Fin de la démo après le dernier puzzle de la liste.");
            }

            // 5) Restaurer l'UI rewind à son état précédent
            if (rewindUIRoot) rewindUIRoot.SetActive(prevRewindActive);

            // 6) Fade back in
            if (fadeCanvas) yield return StartCoroutine(FadeRoutine(0f, fadeDuration));

            isTransitioning = false;
        }

        private IEnumerator FadeRoutine(float target, float duration)
        {
            if (!fadeCanvas || duration <= 0f)
            {
                if (fadeCanvas) fadeCanvas.alpha = target;
                yield break;
            }
            float start = fadeCanvas.alpha;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                fadeCanvas.alpha = Mathf.Lerp(start, target, t / duration);
                yield return null;
            }
            fadeCanvas.alpha = target;
        }
    }
}

