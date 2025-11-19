using UnityEngine;

public class TriggerPadVisual : MonoBehaviour
{
    [SerializeField] private Animator _animator;
    [SerializeField] private bool trembling;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public void PressAnim()
    {
        _animator.SetTrigger("Pressed");
    }

    public void ResetAnim()
    {
        trembling = false;
        _animator.SetTrigger("Reset");
    }

    public void StartTremble()
    {
        if (!trembling)
        {
            trembling = true;
            _animator.SetTrigger("Rearming");
        }
    }

    public void StopTremble()
    {
        if (trembling)
        {
            trembling = false;
            _animator.SetTrigger("CancelRearming");
        }
    }
}
