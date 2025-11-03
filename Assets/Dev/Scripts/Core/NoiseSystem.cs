// FILE: Assets/Dev/Scripts/Core/NoiseSystem.cs
//
// Rôle (résumé)
// - Bus d’événements global pour propager des “bruits” de gameplay (levier, piège, etc.).
// - Les NME s’y abonnent pour être alertés (ex. passer en état de poursuite).
//
// Invariants
// - API publique inchangée: événement 'NoiseRaised' et méthode statique 'Emit(Vector2Int at)'.

using System;
using UnityEngine;

namespace Sarabande.Core
{
    /// <summary>
    /// Bus global pour la diffusion de "bruits" de gameplay. Les systèmes producteurs
    /// (leviers, dalles, pièges, etc.) appellent <see cref="Emit(UnityEngine.Vector2Int)"/>
    /// et les auditeurs intéressés (p. ex. les NME) s'abonnent à <see cref="NoiseRaised"/>.
    /// </summary>
    public static class NoiseSystem
    {
        /// <summary>
        /// Événement déclenché lorsqu'un bruit est émis à une cellule donnée.
        /// Les abonnés reçoivent la position logique <c>Vector2Int</c> sur la grille.
        /// </summary>
        public static event Action<Vector2Int> NoiseRaised;

        /// <summary>
        /// Émet un bruit à la cellule <paramref name="at"/> (portée globale pour le proto).
        /// Tous les abonnés à <see cref="NoiseRaised"/> sont notifiés immédiatement.
        /// </summary>
        /// <param name="at">Cellule de la grille où le bruit est produit.</param>
        public static void Emit(Vector2Int at)
        {
            NoiseRaised?.Invoke(at);
        }
    }
}
