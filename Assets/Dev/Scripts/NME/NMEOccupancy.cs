using System.Collections.Generic;
using UnityEngine;

namespace Sarabande.NME
{
    /// <summary>
    /// Registry global (scène) des positions de tous les NME.
    /// Permet de savoir si une case est occupée à l’instant T.
    /// </summary>
    public static class NMEOccupancy
    {
        // Nous stockons la cellule courante de chaque NME
        private static readonly Dictionary<NMEController, Vector2Int> _cells = new();

        /// <summary> Appelé par chaque NME au démarrage/activation. </summary>
        public static void Register(NMEController nme, Vector2Int initialCell)
        {
            _cells[nme] = initialCell;
        }

        /// <summary> Appelé quand un NME disparaît (destroy/disable définitif). </summary>
        public static void Unregister(NMEController nme)
        {
            _cells.Remove(nme);
        }

        /// <summary> Appelé par un NME quand sa cellule change (Start, Reset, fin de step…). </summary>
        public static void UpdateCell(NMEController nme, Vector2Int cell)
        {
            if (nme == null) return;
            _cells[nme] = cell;
        }

        /// <summary>
        /// True si au moins un autre NME occupe exactement cette case.
        /// </summary>
        public static bool IsOccupied(Vector2Int cell, NMEController exclude = null)
        {
            foreach (var kv in _cells)
            {
                if (exclude != null && kv.Key == exclude) continue;
                if (kv.Value == cell) return true;
            }
            return false;
        }

        /// <summary>
        /// Nombre d’occupants sur cette case (debug/diagnostic).
        /// </summary>
        public static int CountAt(Vector2Int cell)
        {
            int c = 0;
            foreach (var kv in _cells) if (kv.Value == cell) c++;
            return c;
        }
    }
}
