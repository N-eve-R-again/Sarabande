using Sarabande.Levels;
using System.Collections.Generic;
using UnityEngine;
using Sarabande.Triggerables;
using Sarabande.EntityExtensions;

public class DiscoSequenceEntity : MonoBehaviour, ITriggerable, ISignaler
{
    public DiscoSequenceConfig config;
    public string[] successTriggerableKeys;
    public string[] failTriggerableKeys ;
    public List<DalleDiscoEntity> dalles;
    public int progress = 0;

    public TriggerableData triggerableData => config;

    public void Init(DiscoSequenceConfig _config)
    {
        config = _config;
        
        progress = 0;
        successTriggerableKeys = config.successTriggerKeys;
        failTriggerableKeys = config.failTriggerKeys;

    }
    public void GetDalles(List<DalleDiscoEntity> dalleDiscos) => dalles = dalleDiscos;

    public void Trigger()
    {
        /*foreach (var item in dalles)
        {
            item.SequenceStarted();
        }*/
        Debug.Log("DISCO START");
        progress = 0;
        dalles[0].SequenceStarted();
        dalles[0].Activate();
    }

    public void Fail()
    {
        foreach (var item in dalles)
        {
            item.FailState();
        }
        this.SendSignal(failTriggerableKeys);

        this.SendTriggerableCallback();
    }

    public void PreviewNext()
    {
        if (progress + 1 < dalles.Count) dalles[progress + 1].SequenceStarted();
    }

    public void Success(DalleDiscoEntity disco) 
    {
        progress++;

        if (progress >= dalles.Count)
        {
            this.SendSignal(successTriggerableKeys);
        }
        else
        {
            dalles[progress].Activate();
        }
    }

}
