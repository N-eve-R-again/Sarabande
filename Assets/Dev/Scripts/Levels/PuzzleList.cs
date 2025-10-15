using System.Collections.Generic;
using UnityEngine;

namespace Sarabande.Levels
{
    /// <summary>
    /// Liste ordonnée des LevelData à enchaîner dans ce cercle.
    /// Éditable dans l’inspecteur.
    /// </summary>
    [CreateAssetMenu(fileName = "PuzzleList", menuName = "Sarabande/Puzzle List")]
    public class PuzzleList : ScriptableObject
    {
        public List<LevelData> levels = new(); // remplis dans l’inspecteur
    }
}

