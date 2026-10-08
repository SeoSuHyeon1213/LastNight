using System;
using UnityEngine;

// 이동·탐지·체력·적 등록은 Zombie를 사용하고, 개별 능력의 생명주기를 관리한다.
public abstract class SpecialZombie : Zombie, ISpecialZombie, ISpecialAbility {
    [SerializeField] private bool lockMovementDuringAbility = true;

    public SpecialAbilityState AbilityState { get; private set; } = SpecialAbilityState.Ready;
    public int SuccessfulAbilityUses { get; private set; }
    public event Action<ISpecialAbility> AbilityExecuted;

    private bool executionRecorded;
    private bool changingAbility;

    protected override bool ListensToNoise => false;
    public bool CanUseAbility => !changingAbility && isActiveAndEnabled && !dead &&
        AbilityState == SpecialAbilityState.Ready && !IsAttacking && IsAgentReady &&
        CanAdvanceAbility && CanBeginAbility;

    private bool CanAdvanceAbility => Time.timeScale > 0f &&
        (GameManager.instance == null || !GameManager.instance.isGameover) &&
        (GameSessionManager.instance == null ||
            (!GameSessionManager.instance.IsPaused && !GameSessionManager.instance.IsRebinding));

    public void SpecialAttack() {
        TryUseAbility();
    }

    public bool TryUseAbility() {
        if (!CanUseAbility) return false;
        executionRecorded = false;
        AbilityState = SpecialAbilityState.Active;
        if (lockMovementDuringAbility) SetMovementLocked(true);
        OnAbilityStarted();
        return true;
    }

    // 예고 시작이 아니라 실제 폭발·소환·타격 성공 시 파생 클래스가 호출한다.
    protected bool RecordAbilityExecution() {
        if (AbilityState != SpecialAbilityState.Active || executionRecorded ||
            dead || !isActiveAndEnabled || !CanAdvanceAbility) return false;
        executionRecorded = true;
        SuccessfulAbilityUses++;
        AbilityExecuted?.Invoke(this);
        return true;
    }

    protected void CompleteAbility() {
        if (AbilityState != SpecialAbilityState.Active) return;
        AbilityState = SpecialAbilityState.Ready;
        if (lockMovementDuringAbility) SetMovementLocked(false);
    }

    public void CancelAbility() {
        if (AbilityState != SpecialAbilityState.Active || changingAbility) return;
        changingAbility = true;
        AbilityState = SpecialAbilityState.Ready;
        try {
            OnAbilityCancelled();
        } finally {
            if (lockMovementDuringAbility) SetMovementLocked(false);
            changingAbility = false;
        }
    }

    protected override void OnEnable() {
        base.OnEnable();
        AbilityState = SpecialAbilityState.Ready;
        SuccessfulAbilityUses = 0;
        executionRecorded = false;
        changingAbility = false;
        OnAbilityReset();
    }

    protected override void OnDisable() {
        CancelAbility();
        base.OnDisable();
    }

    public override void Die() {
        if (dead) return;
        // 기존 onDeath 콜백에 통지하기 전에 예고·예약·효과를 정리한다.
        changingAbility = true;
        bool wasActive = AbilityState == SpecialAbilityState.Active;
        AbilityState = SpecialAbilityState.Dead;
        try {
            if (wasActive) OnAbilityCancelled();
            base.Die();
        } finally {
            changingAbility = false;
        }
    }

    protected virtual void LateUpdate() {
        if (AbilityState != SpecialAbilityState.Active || dead) return;
        if (GameManager.instance != null && GameManager.instance.isGameover) {
            CancelAbility();
            return;
        }
        if (CanAdvanceAbility) TickAbility(Time.deltaTime);
    }

    // 거리·목표·재사용 대기시간은 각 종류에서 판정한다.
    protected abstract bool CanBeginAbility { get; }
    protected abstract void OnAbilityStarted();
    protected abstract void TickAbility(float deltaTime);
    protected abstract void OnAbilityCancelled();
    protected virtual void OnAbilityReset() { }
}
