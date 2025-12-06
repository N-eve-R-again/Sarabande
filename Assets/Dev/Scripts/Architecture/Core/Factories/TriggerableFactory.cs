using Sarabande.Core;
using Sarabande.Levels;
using System.Collections.Generic;
using UnityEngine;

public class TriggerableFactory : MonoBehaviour, IClearable
{
    [Header("GameObject Folders")]
    private Transform arrowTrapsParent;
    private Transform gatesParent;
    private Transform triggerableFolder;

    [Header("Prefabs")]
    [SerializeField] private GameObject arrowTrapPrefab;
    [SerializeField] private GameObject gatePrefab;
    [SerializeField] private GameObject discoSeqPrefab;
    [SerializeField] private GameObject discoDallePrefab;

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

        foreach (var triggerable in _levelData.triggerables)
        {
            switch (triggerable)
            {
                case ArrowTrapConfig arrowTrap:
                    CreateArrowTrap(arrowTrap);
                    break;
                case GateConfig gate:
                    CreateGate(gate);
                    break;


            }
        }

        foreach (var disco in _levelData.discoSequencesConfigs)
        {
            CreateDiscoSeq(disco);
        }

        //arrow traps
        //doors et timed doors
        //grilles

        jobDone = true; 

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
        entity.Init(config,dalles);
    }
    private void CreateGate(GateConfig gate)
    {

        GameObject temp = Instantiate(gatePrefab, gatesParent);
        GateEntity entity = temp.GetComponent<GateEntity>();
        entity.Init(gate, $"Gate_{gate.cell.ToString()}");
    }

    private void CreateArrowTrap(ArrowTrapConfig config)
    {

            GridCoord c = config.cell;
            GameObject temp = Instantiate(arrowTrapPrefab, arrowTrapsParent);
            ArrowTrapEntity entity = temp.GetComponent<ArrowTrapEntity>();

            entity.Init(config, $"ArrowTrap_{c.ToString()}");

    }
}
