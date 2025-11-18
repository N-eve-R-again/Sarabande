using Sarabande.Core;
using Sarabande.Levels;
using UnityEngine;

public class ListenerCreationHelper
{
    public static void SetupListenerEntity(
        MonoBehaviour mono,// Le MonoBehaviour (pour transform, etc.)
        IListener listener,// L'interface
        GridCoord cell,// La case de la grille
        string name// Le nom
        )                  
    {
        // 1. Nommer l'objet
        mono.gameObject.name = name;
        mono.transform.position = GridUtils.CenterXZ(cell);

        // 5. Notifier via events (pas de référence au manager!)
        
        LevelEntityEvents.NotifyListenerRegistry(cell, listener);
    }


}

