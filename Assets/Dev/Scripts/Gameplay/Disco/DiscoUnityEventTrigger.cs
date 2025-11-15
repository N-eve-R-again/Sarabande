using UnityEngine;

namespace Sarabande.Disco
{
    /// <summary>
    /// Pont générique : appelle DiscoSequenceSystem.StartSequence(...)
    /// depuis n'importe quel UnityEvent (Button.onClick, Trigger.onEnter, etc.).
    /// </summary>
    public class DiscoUnityEventTrigger : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private DiscoSequenceSystem disco; // assigne ton DiscoSequenceSystem
        [SerializeField] private int sequenceIndex = 0;     // séquence à lancer par défaut

        /// <summary>
        /// Méthode sans paramètre — pratique pour la plupart des UnityEvents.
        /// </summary>
        public void OnEvent()
        {
            if (!disco) return;
            disco.StartSequence(sequenceIndex);
        }

        /// <summary>
        /// Variante avec int — utile si ta source UnityEvent envoie un entier.
        /// </summary>
        public void OnEventInt(int index)
        {
            if (!disco) return;
            disco.StartSequence(index);
        }

        /// <summary>
        /// Variante avec bool — lance seulement si true (pratique pour toggles/triggers).
        /// </summary>
        public void OnEventBool(bool on)
        {
            if (on) OnEvent();
        }

        /// <summary>
        /// Variante avec string — parse un index si possible, sinon utilise l’index par défaut.
        /// </summary>
        public void OnEventString(string value)
        {
            if (!disco) return;
            if (int.TryParse(value, out var idx)) disco.StartSequence(idx);
            else disco.StartSequence(sequenceIndex);
        }
    }
}
