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

        [Header("Factories")]
        [SerializeField] private StaticVisualsFactory staticVisualsFactory;
        [SerializeField] private ListenerFactory listenerFactory;
        [SerializeField] private TriggerableFactory triggerableFactory;

        [Header("Debug")]
        [SerializeField, HideInInspector] private Sarabande.Levels.LevelData levelData;

        private void Awake()
        {
            if (levelData == null)
            {
                Debug.LogError("[LevelLoader] LevelData manquant.");
                return;
            }

            entitiesManager.Ready(levelData);

            staticVisualsFactory.BuildStaticVisuals(levelData); //Walls, ThinWalls, Grid
            triggerableFactory.BuildTriggerables(levelData);//ArrowTraps, Doors, Disco, Grilles
            listenerFactory.BuildListeners(levelData); //Message, FakeWalls, Exit, Messages

            BuildActors();//Player, NMEs

            Debug.Log("[LevelLoader] Build terminé.");
        }

        private void BuildActors()
        {
            //player
            //enemies
        }



    }
}
