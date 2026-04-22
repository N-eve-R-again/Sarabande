using Sarabande.Core;
using Sarabande.Levels;
using System.Collections.Generic;
using UnityEngine;
using Sarabande.Triggerables;
using Sarabande.Actors;

public class TriggerableFactory : MonoBehaviour, IClearable
{
    [Header("GameObject Folders")]
    
    private Transform arrowTrapsParent;
    private Transform levelEnterExitParent;
    private Transform gatesParent;
    private Transform triggerableFolder;

    private int triggeablecount = 0;
    private Transform ParentOfAll;

    [Header("Prefabs")]
    [SerializeField] private GameObject arrowTrapPrefab;
    [SerializeField] private GameObject gatePrefab;
    [SerializeField] private GameObject discoSeqPrefab;
    [SerializeField] private GameObject discoDallePrefab;
    [SerializeField] private GameObject ExitPrefab;
    [SerializeField] private GameObject EnterPrefab;

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

        levelEnterExitParent = new GameObject("Enter Exit").transform;
        levelEnterExitParent.SetParent(triggerableFolder, false);

        gatesParent = new GameObject("Gates").transform;
        gatesParent.SetParent(triggerableFolder, false);

        triggerableFolder.SetParent(ParentOfAll);
    }
    public int BuildTriggerables(LevelData _levelData, Transform parent)
    {
        triggeablecount = 0;
        ParentOfAll = parent;
        CreateFolders();

        foreach (var triggerable in _levelData.triggerables)
        {
            switch (triggerable)
            {
                case ArrowTrapConfig arrowTrap:
                    CreateArrowTrap(arrowTrap);
                    triggeablecount++;
                    break;
                case GateConfig gate:
                    CreateGate(gate);
                    triggeablecount++;
                    break;


            }
        }

        foreach (var disco in _levelData.discoSequencesConfigs)
        {
            CreateDiscoSeq(disco);
            triggeablecount++;
        }

        BuildEnterExit(_levelData);

        //arrow traps
        //doors et timed doors
        //grilles

        jobDone = true; 
        return triggeablecount;

    }
    private void BuildEnterExit(LevelData _levelData)
    {
        GameObject temp2 = Instantiate(EnterPrefab, levelEnterExitParent);
        temp2.name = "Enter Door";
        EnterDoorEntity enterDoorEntity = temp2.GetComponent<EnterDoorEntity>();
        enterDoorEntity.Sync(_levelData.hero);
        enterDoorEntity.SyncVisual();
        triggeablecount++;

        GameObject temp = Instantiate(ExitPrefab, levelEnterExitParent);
        temp.name = "Exit Door";
        ExitDoorEntity exitDoorEntity = temp.GetComponent<ExitDoorEntity>();
        exitDoorEntity.Sync(_levelData.exit);
        exitDoorEntity.SyncVisual();
        triggeablecount++;
    }
    private void CreateDiscoSeq(DiscoSequenceConfig config)
    {

        GameObject temp = Instantiate(discoSeqPrefab);
        temp.name = config.triggerKey;
        List<DalleDiscoEntity> dalles = new();
        DiscoSequenceEntity entity = temp.GetComponent<DiscoSequenceEntity>();
        int i = 0;
        foreach (var item in config.cells)
        {
            GameObject dalleob = Instantiate(discoDallePrefab, temp.transform);
            dalleob.name = "DalleDisco " + i;
            DalleDiscoEntity dalle = dalleob.GetComponent<DalleDiscoEntity>();
            dalle.Init(item, entity);
            dalles.Add(dalle);
            i++;
        }
        entity.Init(config);
        entity.GetDalles(dalles);
    }
    private void CreateGate(GateConfig gate)
    {

        GameObject temp = Instantiate(gatePrefab, gatesParent);
        GateEntity entity = temp.GetComponent<GateEntity>();
        entity.Sync(gate, $"Gate_{gate.cell.ToString()}");
        entity.SyncVisual();
    }

    private void CreateArrowTrap(ArrowTrapConfig config)
    {

            GridCoord c = config.cell;
            GameObject temp = Instantiate(arrowTrapPrefab, arrowTrapsParent);
            ArrowTrapEntity entity = temp.GetComponent<ArrowTrapEntity>();

            entity.Init(config, $"ArrowTrap_{c.ToString()}");

    }
}
