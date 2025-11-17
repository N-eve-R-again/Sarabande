// FILE: Assets/DEV/Scripts/Traps/ArrowTrapVisuals.cs
using System.Collections.Generic;
using UnityEngine;
using Sarabande.Core;     // EdgeDirection
using Sarabande.Levels;

namespace Sarabande.Traps
{
    /// <summary>
    /// Visuels de pièges :
    /// - dalles (légèrement relevées, s'enfoncent à l'activation)
    /// - marqueurs horizontaux sur le HAUT des murs/bords pouvant tirer (centrés, sans indiquer la direction)
    /// À attacher sur LevelRoot. Purement visuel.
    /// </summary>
    public class ArrowTrapVisuals : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private LevelContext levelContext;
        [SerializeField, HideInInspector] private LevelData levelData;
        [SerializeField] private bool includeDiscoStartTiles = true;

        private Transform _tilesParent;
        private HashSet<Vector2Int> _discoCells;

        private void Awake()
        {
            if (levelData == null) { Debug.LogError("[ArrowTrapVisuals] LevelData manquant."); enabled = false; return; }

            _tilesParent = new GameObject("TrapTiles").transform;
            _tilesParent.SetParent(transform, false);

            BuildDiscoCellSet();
        }



        private void BuildDiscoCellSet()
        {
            _discoCells = new HashSet<Vector2Int>();
            if (levelData == null) return;

            // 1) Toutes les cases des séquences disco
            if (levelData.discoSequences != null)
            {
                foreach (var seq in levelData.discoSequences)
                {
                    if (seq?.cells == null) continue;
                    foreach (var c in seq.cells)
                        _discoCells.Add(new Vector2Int(c.x, c.z));
                }
            }

            // 2) Optionnel : cases “start” disco
            if (includeDiscoStartTiles && levelData.discoStartTiles != null)
            {
                foreach (var c in levelData.discoStartTiles)
                    _discoCells.Add(new Vector2Int(c.x, c.z));
            }
        }

        private bool IsDiscoCell(Vector2Int cell)
        {
            return _discoCells != null && _discoCells.Contains(cell);
        }

        // --- Utils ---
       


    }
}
