using Sarabande.Core;
using System.Collections.Generic;
using UnityEngine;

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
        public bool ready = false;

        [Header("Factories")]
        [SerializeField] private StaticVisualsFactory staticVisualsFactory;
        [SerializeField] private ListenerFactory listenerFactory;
        [SerializeField] private TriggerableFactory triggerableFactory;
        [SerializeField] private ActorFactory actorFactory;
        [SerializeField] private LevelEditor levelEditor;

        public void Construct(Transform Root)
        {
            LogGen.LogAs(this, "Found Level Data", "white");
            LevelData levelData = levelEditor.levelData;
            ready = false;

            if (levelData == null)
            {
                Debug.LogError("[LevelLoader] LevelData manquant.");
                return;
            }
            int act = 0;
            int lis = 0;
            int tri = 0;
            int obs = 0;
            LogGen.LogAs(this, "Started Construct", "orange");

            obs = staticVisualsFactory.BuildStaticVisuals(levelData, Root); //Walls, ThinWalls, Grid
            tri = triggerableFactory.BuildTriggerables(levelData, Root);//ArrowTraps, Doors, Disco, Grilles
            lis = listenerFactory.BuildListeners(levelData, Root); //Message, FakeWalls, Exit, Messages
            act = actorFactory.BuildActors(levelData, Root); //Hero, Nmes


            LogGen.LogAs(this, $"{obs} Obstacles Created");
            LogGen.LogAs(this, $"{lis} Listeners Created");
            LogGen.LogAs(this, $"{tri} Triggerables Created");
            LogGen.LogAs(this, $"{act} Actors Created");

            LogGen.LogAs(this, "Finished Construct", "green");

            ready = true;
        }



    }
}
