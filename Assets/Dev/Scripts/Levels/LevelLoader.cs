using Sarabande.Core;
using System.Collections.Generic;
using System.Drawing;
using UnityEngine;
using static UnityEditor.PlayerSettings;

namespace Sarabande.Levels
{
    public static class LevelGlobalSettings
    {
        [Header("Cells")]
        [SerializeField, Min(0.001f)] public static float cellSize = 1f;

        [Header("Layers")]
        [SerializeField] public static string obstaclesLayerName = "Obstacles";

        public static void SetLayerForObstacle(GameObject gameObject)
        {
            int obsLayer = LayerMask.NameToLayer(LevelGlobalSettings.obstaclesLayerName);
            if (obsLayer != -1) gameObject.layer = obsLayer;
            else Debug.LogWarning($"[LevelLoader] Layer '{LevelGlobalSettings.obstaclesLayerName}' introuvable.");
        }
    }

    /// <summary>
    /// Construit la grille visible, les murs (non-walkables) et les murs fins à partir d'un LevelData.
    /// À attacher sur LevelRoot dans la scène.
    /// </summary>
    public class LevelLoader : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private LevelContext levelContext;
        [SerializeField] private LevelEntitiesManager entitiesManager;

        [Space]
        [SerializeField] private StaticVisualsFactory staticVisualsFactory;
        [SerializeField] private StaticListenerFactory staticListenerFactory;

        [Header("Debug")]
        [SerializeField] private bool useLevelContext = true;
        [SerializeField, HideInInspector] private Sarabande.Levels.LevelData levelData;
 
        private void Awake()
        {
            if (levelData == null)
            {
                Debug.LogError("[LevelLoader] LevelData manquant.");
                return;
            }

            entitiesManager.Ready();

            staticVisualsFactory.BuildStaticVisuals(levelData); //Walls, ThinWalls, Grid
            staticListenerFactory.BuildStaticListeners(levelData); //Message, FakeWalls, Exit, Messages

            BuildActors();//Player, NMEs

            Debug.Log("[LevelLoader] Build terminé.");
        }

        private void BuildActors()
        {
            //player
            //enemies
        }
        
        private void AttachContext()
        {
            if (!useLevelContext) return;

            if (!levelContext)
                levelContext = GetComponentInParent<Sarabande.Core.LevelContext>();

            if (levelContext != null)
            {
                levelContext.LevelDataChanged += HandleContextLevelDataChanged;
                HandleContextLevelDataChanged(levelContext.LevelData); // init immédiate
            }
            else
            {
                Debug.LogWarning($"[{GetType().Name}] Aucun LevelContext parent trouvé.");
            }
        }

        private void DetachContext()
        {
            if (levelContext != null)
                levelContext.LevelDataChanged -= HandleContextLevelDataChanged;
        }

        private void HandleContextLevelDataChanged(Sarabande.Levels.LevelData ld)
        {
            if (levelData == ld) return;
            levelData = ld;
#if UNITY_EDITOR
            if (!Application.isPlaying)
                UnityEditor.EditorUtility.SetDirty(this); // l’inspector reflète la maj auto
#endif
        }
        private void OnEnable() { AttachContext(); }
        private void OnDisable() { DetachContext(); }
#if UNITY_EDITOR
        private void OnValidate() { if (!Application.isPlaying) AttachContext(); }
#endif
    }
}
