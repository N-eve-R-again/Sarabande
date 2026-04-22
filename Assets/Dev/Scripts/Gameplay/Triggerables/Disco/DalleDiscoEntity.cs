using Sarabande.Core;
using Sarabande.EntityExtensions;

using UnityEngine;
using UnityEngine.UI;

public class DalleDiscoEntity : MonoBehaviour, ISensor
{
    public enum DiscoState
    {
        Desactivated,
        Standby,
        WaitForPlayer,
        Filling,
        Validated,

    }

    [SerializeField] private InteractionLayer interactsWith;
    [SerializeField] private DiscoSequenceEntity sequenceEntity;
    [SerializeField] private bool occupied;
    [SerializeField] private bool isActive;
    [SerializeField] private bool collected = false;
    [SerializeField] private Animator animator;
    [SerializeField] private Image validateFilledge;
    [SerializeField] private Image validateFillCenter;
    [SerializeField] private Image edgeFill;

    public float timertoreach = 3f;
    public float timertoactivate = 3f;
    public float timetofill = 1f;
    private float timer;


    public DiscoState state = DiscoState.Desactivated;
    public bool IsActive => state != DiscoState.Desactivated && state != DiscoState.Validated;
    bool ISensor.occupied => occupied;

    public InteractionLayer interactionLayer => interactsWith;

    public void Init(Vector2Int cell, DiscoSequenceEntity _sequenceEntity)
    {
        sequenceEntity = _sequenceEntity;
        transform.position = GridUtils.CenterXZ(cell);
        RegistryEvents.NotifySensorRegistry(cell,this);

        timer = 2f;
    }

    private void Success()
    {
        state = DiscoState.Validated;
        animator.SetTrigger("Validate");
        validateFillCenter.fillAmount = 1;
        validateFilledge.fillAmount = 1;
        sequenceEntity.Success(this);
    }

    public void Failed() => sequenceEntity.Fail();//dire à maman qu'on a échoué

    public void FailState()
    {
        validateFillCenter.fillAmount = 1;
        validateFilledge.fillAmount = 1;
        if(state != DiscoState.Desactivated) animator.SetTrigger("Fail");

        state = DiscoState.Desactivated;
        timer = 0;
    }

    
    public void SequenceStarted()
    {
        animator.SetTrigger("Awake");
        state = DiscoState.Standby;
        validateFillCenter.fillAmount =0;
        validateFilledge.fillAmount = 0;
    }

    public void Activate()
    {
        if (state == DiscoState.Standby)
        {
            state = DiscoState.WaitForPlayer;
            isActive = true;
            animator.SetTrigger("Activate");
            timer = timertoreach + timertoactivate;
        }
    }

    public void Deactivate()
    {
        isActive = false;
        occupied = false;
    }

    public void OnEnter()
    {
        if (state == DiscoState.WaitForPlayer)
        {
            animator.SetTrigger("PlayerIn");
            sequenceEntity.PreviewNext();
            state = DiscoState.Filling;
            timer = 0f;
            edgeFill.fillAmount = 1;

        }

    }

    public void OnExit()
    {
        if (state == DiscoState.Filling) {
            Failed();
        }

    }

    private void Update()
    {
        if (state == DiscoState.WaitForPlayer) {
            if(timer > timertoreach)
            {
                timer -= Time.deltaTime;
                edgeFill.fillAmount = 1f;
            }
            if(timer >= 0)
            {
                timer -= Time.deltaTime;
                edgeFill.fillAmount = timer / timertoreach;
            }
            else
            {
                Failed();
            }
        
        }
        if(state == DiscoState.Filling)
        {
            if(timer < timetofill)
            {
                timer += Time.deltaTime;
                validateFillCenter.fillAmount = timer / timetofill;
                validateFilledge.fillAmount = timer / timetofill;
            }
            else
            {
                Success();
            }

        }
    }
}
