using System;
using UnityEngine;

namespace Sarabande.Core
{
    /// <summary>Bus global pour “bruits” de gameplay (levier, piège, etc.). Ils doivent pouvoir alerter les NME</summary>
    public static class NoiseSystem
    {
        public static event Action<Vector2Int> NoiseRaised;

        /// <summary>Émet un bruit à une cellule (rayon illimité pour le proto).</summary>
        public static void Emit(Vector2Int at)
        {
            NoiseRaised?.Invoke(at);
        }
    }
}
