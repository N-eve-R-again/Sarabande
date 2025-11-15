using UnityEngine;

public class TestTriggerable : MonoBehaviour, ITriggerable
{
    public GameObject GameObject => gameObject;

    public int triggerableKey;

    int ITriggerable.triggerableKey { get => triggerableKey;}

    public void Init()
    {
        triggerableKey = 0;
    }

    public void Trigger()
    {
        Debug.Log("CE TRIGGER FONCTIONNE C'EST FOU");
    }
}
