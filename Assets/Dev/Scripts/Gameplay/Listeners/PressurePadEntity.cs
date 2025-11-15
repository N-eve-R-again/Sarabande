using Sarabande.Core;
using UnityEngine;

public class PressurePadEntity : MonoBehaviour, IListener, IResettable
{

    public int triggerKey = -1;
    public bool armed = true;

    public Vector2Int gridPosition;

    public IListener.ListenerType type => IListener.ListenerType.PressurePad;
    Vector2Int IListener.gridCoord => gridPosition;

    public void Init(GridCoord _coord, string _name)
    {
        gameObject.name = _name;
        gridPosition = (Vector2Int)_coord;

        IListener.RegisterListener(this);
        IListener.RegisterLinkToTrigger(triggerKey,this);
    }

    public void OnExitInteract()
    {
        throw new System.NotImplementedException();
    }

    public void OnInteract()
    {
        if (armed)
        {
            armed = false;
            //visuals,
            //sound

        }

    }

    public void ResetToInitial()
    {
        throw new System.NotImplementedException();
    }


}
