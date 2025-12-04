using Sarabande.Core;
using Sarabande.Levels;
using UnityEngine;
using static UnityEngine.Rendering.STP;

public class TriggerableFactory : MonoBehaviour, IClearable
{
    [Header("GameObject Folders")]
    private Transform arrowTrapsParent;
    private Transform gatesParent;
    private Transform triggerableFolder;

    [Header("Prefabs")]
    [SerializeField] private GameObject arrowTrapPrefab;
    [SerializeField] private GameObject gatePrefab;

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

        gatesParent = new GameObject("Gates").transform;
        gatesParent.SetParent(triggerableFolder, false);

    }
    public void BuildTriggerables(LevelData _levelData)
    {
        CreateFolders();
        CreateArrowTraps(_levelData);
        CreateGates(_levelData);
        //arrow traps
        //doors et timed doors
        //grilles

        jobDone = true; 

    }
    private void CreateGates(LevelData _levelData)
    {
        if (_levelData.gates == null) return;

        foreach (var gate in _levelData.gates)
        {
            GameObject temp = Instantiate(gatePrefab, gatesParent);
            GateEntity entity = temp.GetComponent<GateEntity>();

            entity.Init(gate, $"Gate_{gate.cell.ToString()}");

        }
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
