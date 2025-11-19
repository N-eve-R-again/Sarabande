using Sarabande.Core;
using Unity.VisualScripting;
using UnityEngine;

public class ArrowTrapVisual : MonoBehaviour
{
    public Transform trap;
    public GameObject arrow;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public void InitVisual(CardinalDirection dir)
    {
        SetRotation(dir);
    }

    public void SetArmed(bool armed)
    {
        arrow.SetActive(armed);
    }

    private void SetRotation(CardinalDirection dir)
    {
        float yRot = 0f;
        switch (dir)
        {
            case CardinalDirection.North:
                yRot = 0f; break;
            case CardinalDirection.South: yRot = 180f; break;
            case CardinalDirection.East: yRot = 90f; break;
            case CardinalDirection.West: yRot = 270f; break;

        }
        transform.rotation = Quaternion.Euler(0f, yRot, 0f);
    }
}
