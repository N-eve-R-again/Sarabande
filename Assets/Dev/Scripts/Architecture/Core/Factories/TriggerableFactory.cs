using Sarabande.Core;
using Sarabande.Levels;
using UnityEngine;

public class TriggerableFactory : MonoBehaviour, IClearable
{
    [Header("GameObject Folders")]
    private Transform arrowTrapsParent;

    [Header("Prefabs")]
    [SerializeField] private GameObject arrowTrapPrefab;

    [SerializeField] private bool jobDone = false;

    public void ClearObject()
    {
        throw new System.NotImplementedException();
    }
    private void CreateFolders()
    {
        arrowTrapsParent = new GameObject("ArrowTraps").transform;
        arrowTrapsParent.SetParent(transform, false);

    }
    public void BuildTriggerables(LevelData _levelData)
    {
        CreateFolders();
        CreateArrowTraps(_levelData);
        //arrow traps
        //doors et timed doors
        //grilles
        
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
