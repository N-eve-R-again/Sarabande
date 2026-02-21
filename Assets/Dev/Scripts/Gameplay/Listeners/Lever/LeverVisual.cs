using Sarabande.Core;
using UnityEngine;

public class LeverVisual : MonoBehaviour
{
    public Animator animator;
    public Transform pivot;
    public GameObject debug;
    private bool pressed = false;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public void InitVisual(CardinalDirection dir, bool instant)
    {
        SetRotation(dir);
        animator.SetBool("Instant", instant);
    }



    public void PressAnim()
    {
        if (pressed) return;
        pressed = true;
        animator.SetTrigger("Press");
    }

    public void ResetAnim()
    {
        if (!pressed) return;
        pressed = false;
        animator.SetTrigger("Reset");
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
        pivot.rotation = Quaternion.Euler(0f, yRot, 0f);
    }
}
