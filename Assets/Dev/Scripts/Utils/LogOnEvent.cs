using UnityEngine;

namespace Sarabande.Utils
{
    public class LogOnEvent : MonoBehaviour
    {
        // Sans paramètre (simple)
        public void LogNow()
        {
            Debug.Log("[LogOnEvent] Event triggered.");
        }

        // Avec paramètre (tu peux écrire le message dans l’Inspector)
        public void LogMessage(string message)
        {
            Debug.Log(message);
        }
    }
}
