using Sarabande.Levels;
using Sarabande.UI;
using System.Collections.Generic;
using UnityEngine;

namespace Sarabande.Messages
{
    /// <summary>
    /// - Affiche un popup + joue la voix quand HÉRO entre sur une case "message".
    /// - Met à jour le compteur (icône + X/Total).
    /// - Les messages collectés sont DÉFINITIFS (ne reset pas).
    /// À attacher sur LevelRoot.
    /// </summary>
    public class MessageSystem : MonoBehaviour, IClearable
    {
        [Header("UI")]
        [SerializeField] private MessagePopupUI_TMP popupUI;         // UI_Canvas/MessagePopup
        [SerializeField] private MessagesUIController messagesUI;    // UI_Canvas/UI_MessagesRoot
        [SerializeField] private Sprite counterIcon;

        [Header("Completion")]
        [SerializeField] private List<bool> messages;
        [SerializeField] private List<MessageConfig> messagesSpecs;

        [Header("Audio")]
        [SerializeField, Range(0f, 1f)] private float voiceVolume = 0.9f;
        private AudioSource _voice;

        public void SubscribeToEvents()
        {
            UIEvents.OnCollectMsgCollectible += Collect;
            UIEvents.OnRegisterMsgCollectible += Register;

        }
        public void UnSubscribeToEvents()
        {
            UIEvents.OnCollectMsgCollectible -= Collect;
            UIEvents.OnRegisterMsgCollectible -= Register;

        }
        private void OnDestroy()
        {
            UnSubscribeToEvents();
        }

        // --- Collect ---
        public int Register(MessageConfig spec)
        {
            messages.Add(false);
            messagesSpecs.Add(spec);
            return messages.Count -1;
        }

        public void Collect(int index)
        {
            // popup
            messages[index] = true;
            MessageConfig collectedSpec = messagesSpecs[index];
            popupUI?.Show(collectedSpec.text, Mathf.Max(0.1f, collectedSpec.displaySeconds));

            // voix
            if (_voice && collectedSpec.voiceClip)
            {
                if (_voice.isPlaying) _voice.Stop();

                Audio.AudioHub.I?.PlayVoiceAt(

                collectedSpec.voiceClip,
                transform.position, // 2D, la position n'a pas d'importance
                voiceVolume

                );
            }

            (int c, int t) = SyncCounter();
            messagesUI?.SetCounter(c, t);
        }

        public void ClearObject()
        {
            messages.Clear();
            (int c, int t) = SyncCounter(); 
            messagesUI?.SetCounter(c,t);
        }

        private (int collected,int total) SyncCounter()
        {

            int i = 0;
            foreach (bool isCollected in messages)
            {
                if (isCollected) i++;
            }

            return (i,messages.Count);
        }

    }
}
