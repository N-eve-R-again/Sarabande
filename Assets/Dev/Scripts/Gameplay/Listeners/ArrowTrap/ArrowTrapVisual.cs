using Sarabande.Core;
using Unity.VisualScripting;
using UnityEngine;

public class ArrowTrapVisual : MonoBehaviour
{
    public Transform trap;
    public GameObject arrow;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public void InitVisual(EdgeDirection dir)
    {
        SetRotation(dir);
    }

    public void SetArmed(bool armed)
    {
        arrow.SetActive(armed);
    }

    private void SetRotation(EdgeDirection dir)
    {
        float yRot = 0f;
        switch (dir)
        {
            case EdgeDirection.North:
                yRot = 0f; break;
            case EdgeDirection.South: yRot = 180f; break;
            case EdgeDirection.East: yRot = 90f; break;
            case EdgeDirection.West: yRot = 270f; break;

        }
        transform.rotation = Quaternion.Euler(0f, yRot, 0f);
    }
}
