using Sarabande.Core;
using UnityEngine;

public class LeverVisual : MonoBehaviour
{
    public Transform pivot;
    public GameObject debug;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public void InitVisual(CardinalDirection dir)
    {
        SetRotation(dir);
    }

    public void SetActivated(bool activated)
    {
        debug.SetActive(activated);
    }

    private void SetRotation(CardinalDirection dir)
    {

        float yRot = 0f;
        switch (dir)
        {
            case CardinalDirection.South:
                yRot = 0f; break;
            case CardinalDirection.North: yRot = 180f; break;
            case CardinalDirection.West: yRot = 90f; break;
            case CardinalDirection.East: yRot = 270f; break;

        }
        transform.rotation = Quaternion.Euler(0f, yRot, 0f);
    }
}
