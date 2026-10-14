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
        (GameSessionBase.instance == null ||
            (!GameSessionBase.instance.IsPaused && !GameSessionBase.instance.IsRebinding));

    public virtual void SpecialAttack() {
        TryUseAbility();
    }

    public bool TryUseAbility() {
        if (!CanUseAbility || !TryPrepareAbility()) return false;
        executionRecorded = false;
        AbilityState = SpecialAbilityState.Active;
        if (LocksMovementDuringAbility) SetMovementLocked(true);
        try {
            OnAbilityStarted();
        } catch {
            CancelAbility();
            throw;
        }
        return true;
    }

    // 예고 시작이 아니라 실제 폭발·소환·타격 성공 시 파생 클래스가 호출한다.
    protected bool RecordAbilityExecution() {
        if (AbilityState != SpecialAbilityState.Active || executionRecorded ||
            dead || !isActiveAndEnabled || !CanAdvanceAbility) return false;
        return RecordExecution();
    }

    // 사망 폭발은 NavMesh·공격·일시정지 상태와 무관한 동기 반응이다.
    // 파생 클래스의 중복 사망 방지 이후, 기본 Die의 사망 통지 전에만 호출한다.
    protected bool RecordDeathAbilityExecution() {
        if (dead || !isActiveAndEnabled || changingAbility || AbilityState == SpecialAbilityState.Dead)
            return false;
        if (AbilityState == SpecialAbilityState.Ready) {
            executionRecorded = false;
            AbilityState = SpecialAbilityState.Active;
        }
        if (executionRecorded) return false;
        return RecordExecution();
    }

    private bool RecordExecution() {
        executionRecorded = true;
        SuccessfulAbilityUses++;
        AbilityExecuted?.Invoke(this);
        return true;
    }

    protected void CompleteAbility() {
        if (AbilityState != SpecialAbilityState.Active) return;
        AbilityState = SpecialAbilityState.Ready;
        if (LocksMovementDuringAbility) SetMovementLocked(false);
    }

    public void CancelAbility() {
        if (AbilityState != SpecialAbilityState.Active || changingAbility) return;
        changingAbility = true;
        AbilityState = SpecialAbilityState.Ready;
        try {
            OnAbilityCancelled();
        } finally {
            if (LocksMovementDuringAbility) SetMovementLocked(false);
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
    protected virtual bool LocksMovementDuringAbility => lockMovementDuringAbility;
    // 준비 실패는 능력 활성화나 성공 횟수를 소비하지 않는다.
    protected virtual bool TryPrepareAbility() { return true; }
    protected abstract bool CanBeginAbility { get; }
    protected abstract void OnAbilityStarted();
    protected abstract void TickAbility(float deltaTime);
    protected abstract void OnAbilityCancelled();
    protected virtual void OnAbilityReset() { }
}
