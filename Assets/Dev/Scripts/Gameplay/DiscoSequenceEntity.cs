using Sarabande.Levels;
using System.Collections.Generic;
using UnityEngine;

public class DiscoSequenceEntity : MonoBehaviour, ITriggerable, ISignalerExtension
{
    public DiscoSequenceConfig config;
    public string[] successTriggerableKeys;
    public string[] failTriggerableKeys ;
    public List<DalleDiscoEntity> dalles;
    public int progress = 0;

    public void Init(DiscoSequenceConfig _config, List<DalleDiscoEntity> dalleDiscos)
    {
        config = _config;
        dalles = dalleDiscos;
        progress = 0;
        successTriggerableKeys = config.successTriggerKeys;
        failTriggerableKeys = config.failTriggerKeys;
        RegistryEvents.NotifyTriggerableRegistry(config.triggerKey, this);
    }
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
        LevelEntityEvents.SendSignal(failTriggerableKeys);
        LevelEntityEvents.NotifyTriggerableCallback(this);
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
            LevelEntityEvents.SendSignal(successTriggerableKeys);
        }
        else
        {
            dalles[progress].Activate();
        }
    }

}
