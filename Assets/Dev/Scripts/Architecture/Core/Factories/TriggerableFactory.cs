using Sarabande.Core;
using Sarabande.Levels;
using UnityEngine;

public class TriggerableFactory : MonoBehaviour, IClearable
{
    [Header("GameObject Folders")]
    private Transform arrowTrapsParent;
    private Transform triggerableFolder;

    [Header("Prefabs")]
    [SerializeField] private GameObject arrowTrapPrefab;

    [SerializeField] private bool jobDone = false;
    public bool IsJobDone() => jobDone;

    public void ClearObject()
    {
        throw new System.NotImplementedException();
    }
    private void CreateFolders()
    {
        triggerableFolder = new GameObject("Triggerables").transform;
        triggerableFolder.SetParent(transform.parent);

        arrowTrapsParent = new GameObject("ArrowTraps").transform;
        arrowTrapsParent.SetParent(triggerableFolder, false);

    }
    public void BuildTriggerables(LevelData _levelData)
    {
        CreateFolders();
        CreateArrowTraps(_levelData);
        //arrow traps
        //doors et timed doors
        //grilles

        jobDone = true; 

    }

    private void CreateArrowTraps(LevelData _levelData)
    {
        if (_levelData.arrowTraps == null) return;

        foreach (ArrowTrapConfig config in _levelData.newArrowTraps)
        {
            GridCoord c = config.cell;
            GameObject temp = Instantiate(arrowTrapPrefab, arrowTrapsParent);
            ArrowTrapEntity entity = temp.GetComponent<ArrowTrapEntity>();

            entity.Init(config, $"ArrowTrap_{c.ToString()}");
        }
    }
}
