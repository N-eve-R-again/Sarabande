using Sarabande.Core;
using UnityEngine;

public class GateVisual : MonoBehaviour
{
    [SerializeField] Animator animator;
    [SerializeField] Transform pivot;
    private bool up = true;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void InitVisual(CardinalDirection dir)
    {
        SetRotation(dir);
    }

    public void DownAnim()
    {
        if (up)
        {
            animator.SetTrigger("Down");
            up = false;
        }

    }

    public void UpAnim()
    {
        if (!up)
        {
            animator.SetTrigger("Up");
            up = true;
        }
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
        pivot.rotation = Quaternion.Euler(0f, yRot, 0f);
    }
}
