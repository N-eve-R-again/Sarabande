using Sarabande.Levels;
using System.Collections.Generic;
using UnityEngine;

public class DiscoSequenceEntity : MonoBehaviour, ITriggerable, ISignalerEnxtension
{
    public DiscoSequenceConfig config;
    public string[] triggerableKeys => config.successTriggerKeys;
    public List<DalleDiscoEntity> dalles;
    public int progress = 0;

    public void Init(DiscoSequenceConfig _config, List<DalleDiscoEntity> dalleDiscos)
    {
        config = _config;
        dalles = dalleDiscos;
        progress = 0;
        RegistryEvents.NotifyTriggerableRegistry(config.triggerKey, this);
    }
    public void Trigger()
    {
        Debug.Log("DISCO START");
        progress = 0;
        dalles[0].Activate();
    }



    public void Success(DalleDiscoEntity disco) 
    {
        dalles[progress].Deactivate();
        progress++;

        if (progress >= dalles.Count)
        {
            LevelEntityEvents.SendSignal(this);
        }
        else
        {
            dalles[progress].Activate();

        }
    }

    // Update is called once per frame
    void Update()
    {

    }
}
