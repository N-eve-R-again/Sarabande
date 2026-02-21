using Sarabande.Core;
using Sarabande.Levels;
using System.Linq;
using UnityEngine;

public static class AbsoluteObjectNamer
{
    public static string GetListenerName(ListenerData data)
    {
        string name = "";
        switch (data)
        {
            case FakeWallData: name = "Fake Wall"; break;

            case MessageConfig: name = "Message"; break;

            case TriggerObjectConfig obj:
                switch (obj.type)
                {
                    case TriggerObjectType.InvisibleTrigger: name = "InvisTrigger";break;
                    case TriggerObjectType.TriggerPad: name = "PressurePad";break;
                    case TriggerObjectType.Lever: name = "Lever";break;
                    default: name = "unknownType"; break;
                }
                break;

            default: name = "unkownType"; break;
        }
        return $"{name} {data.cell}";
    }

}

public class ListenerCreationHelper
{
    public static void SetupListenerEntity(
        MonoBehaviour mono,// Le MonoBehaviour (pour transform, etc.)
        IListener listener,// L'interface
        ListenerData data// La case de la grille
        )                  
    {
        // 1. Nommer l'objet
        mono.gameObject.name = AbsoluteObjectNamer.GetListenerName(data);
        mono.transform.position = GridUtils.CenterXZ(data.cell);

        // 5. Notifier via events (pas de référence au manager!)
        RegistryEvents.NotifyListenerRegistry(data.cell, listener);

        if(data.triggerKeys.Length < 0) return; //pas de keys à register

        foreach (var key in data.triggerKeys)
        {
            RegistryEvents.NotifyTryTriggerLinkRegistry(key, listener);
        }

    }


}

