// FILE: Assets/DEV/Scripts/Core/LevelContext.cs
using UnityEngine;
using Sarabande.Levels;

namespace Sarabande.Core
{
    /// <summary>
    /// Source unique de LevelData pour le niveau.
    /// Diffuse un événement à chaque changement (Editor + Play).
    /// </summary>
    [DisallowMultipleComponent]
    [ExecuteAlways]
    [DefaultExecutionOrder(-1000)]
    public class LevelContext : MonoBehaviour
    {
        [SerializeField] private LevelData levelData;
        public LevelData LevelData
        {
            get => levelData;
            set
            {
                if (levelData == value) return;
                levelData = value;
                RaiseChanged();
            }
        }

        public event System.Action<LevelData> LevelDataChanged;

        private void OnEnable() => RaiseChanged();          // push l'état courant (après reload/enter play)
#if UNITY_EDITOR
        private void OnValidate() => RaiseChanged();         // appelé quand tu changes la valeur dans l’inspector
#endif

        private void RaiseChanged() => LevelDataChanged?.Invoke(levelData);
    }
}
