using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public sealed class DealerZombie : SpecialZombie {
    public enum StrikePhase { Ready, Windup, Recovery, Dead }

    [Header("Heavy Strike")]
    [SerializeField, Min(0.01f)] private float windupSeconds = 0.7f;
    [SerializeField, Min(0.01f)] private float recoverySeconds = 1.2f;
    [SerializeField, Min(0f)] private float cooldownSeconds = 2f;
    [SerializeField, Min(1f)] private float strikeDamageMultiplier = 2f;
    [SerializeField, Min(0.01f)] private float strikeReach = 1.8f;
    [SerializeField, Range(1f, 180f)] private float strikeHalfAngle = 45f;
    [SerializeField, Min(0f)] private float strikeHeight = 1f;
    [SerializeField] private LayerMask strikeBlockingLayers = Physics.DefaultRaycastLayers;

    [Header("Optional Telegraph")]
    [Tooltip("예고 중 표시할 기존 자식 오브젝트를 연결합니다.")]
    [SerializeField] private GameObject windupIndicator;
    [SerializeField] private AudioClip windupSound;
    [Tooltip("예고 시작 시 실행할 Animator Trigger입니다. 비우면 실행하지 않습니다.")]
    [SerializeField] private string attackTrigger = "HasAttack";

    public StrikePhase Phase { get; private set; }
    private PlayerHealth strikeTarget;
    private Collider strikeCollider;
    private Vector3 strikeDirection;
    private float remainingPhaseTime;
    private float nextStrikeTime;
    private NavMeshAgent agent;
    private Animator animator;
    private AudioSource audioSource;
    private bool restoreRotation;
    private bool rotationLocked;
    private bool hasAttackTrigger;

    protected override bool LocksMovementDuringAbility => true;
    protected override bool CanChaseTarget(LivingEntity target) =>
        target is PlayerHealth && base.CanChaseTarget(target);
    protected override bool CanBeginAbility => Phase == StrikePhase.Ready &&
        Time.time >= nextStrikeTime && IsValidTarget() &&
        IsInReach(out _) && !IsBlocked();

    protected override void BeginMeleeAttack(LivingEntity target, Collider collider) {
        if (AbilityState != SpecialAbilityState.Ready || Time.time < nextStrikeTime) return;
        if (target is PlayerHealth player) {
            strikeTarget = player;
            strikeCollider = collider;
            TryUseAbility();
        } else {
            // 거점으로 이동 중이거나 플레이어 접근을 막는 구조물은 기존 공격으로 처리한다.
            base.BeginMeleeAttack(target, collider);
        }
    }

    protected override void OnAbilityReset() {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        audioSource = GetComponent<AudioSource>();
        Phase = StrikePhase.Ready;
        strikeTarget = null;
        strikeCollider = null;
        remainingPhaseTime = 0f;
        nextStrikeTime = 0f;
        rotationLocked = false;
        hasAttackTrigger = false;
        if (animator != null && !string.IsNullOrEmpty(attackTrigger)) {
            foreach (AnimatorControllerParameter parameter in animator.parameters)
                if (parameter.name == attackTrigger && parameter.type == AnimatorControllerParameterType.Trigger)
                    hasAttackTrigger = true;
            if (!hasAttackTrigger)
                Debug.LogWarning("DealerZombie: Attack Trigger가 Animator의 Trigger에 없습니다. 공격 타이머는 동작하지만 예고 애니메이션을 연결해야 합니다.", this);
        }
        SetIndicator(false);
    }

    protected override void OnAbilityStarted() {
        Vector3 direction = strikeTarget.transform.position - transform.position;
        direction.y = 0f;
        strikeDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;
        transform.rotation = Quaternion.LookRotation(strikeDirection);
        if (agent != null) {
            restoreRotation = agent.updateRotation;
            agent.updateRotation = false;
            rotationLocked = true;
        }
        Phase = StrikePhase.Windup;
        remainingPhaseTime = windupSeconds;
        SetIndicator(true);
        if (hasAttackTrigger) animator.SetTrigger(attackTrigger);
        if (windupSound != null && audioSource != null) audioSource.PlayOneShot(windupSound);
    }

    protected override void TickAbility(float deltaTime) {
        remainingPhaseTime -= deltaTime;
        if (remainingPhaseTime > 0f) return;
        if (Phase == StrikePhase.Windup) {
            // 타격 이벤트의 콜백에서 취소/사망해도 예고를 반복 실행하지 않는다.
            Phase = StrikePhase.Recovery;
            remainingPhaseTime = recoverySeconds;
            SetIndicator(false);
            ApplyStrike();
        } else if (Phase == StrikePhase.Recovery) {
            ResetStrike(false);
            CompleteAbility();
        }
    }

    protected override void LateUpdate() {
        base.LateUpdate();
        if (AbilityState == SpecialAbilityState.Active && animator != null)
            animator.SetBool("HasTarget", false);
    }

    private void ApplyStrike() {
        if (!IsValidTarget() || !IsInReach(out Vector3 point) || IsBlocked()) return;
        Vector3 horizontal = point - StrikeOrigin;
        horizontal.y = 0f;
        if (horizontal.sqrMagnitude > 0.0001f &&
            Vector3.Angle(strikeDirection, horizontal) > strikeHalfAngle) return;
        float strikeDamage = Mathf.Round(damage * strikeDamageMultiplier);
        if (strikeDamage <= 0f || float.IsNaN(strikeDamage) || float.IsInfinity(strikeDamage)) return;
        PlayerHealth victim = strikeTarget;
        Vector3 normal = StrikeOrigin - point;
        if (normal.sqrMagnitude < 0.0001f) normal = Vector3.up;
        // 유효 타격을 확정한 시점에 통지한다. 이후 플레이어 사망으로 게임오버가 되어도 누락하지 않는다.
        if (RecordAbilityExecution()) victim.OnDamage(strikeDamage, point, normal.normalized);
    }

    private Vector3 StrikeOrigin => transform.position + Vector3.up * strikeHeight;
    private bool IsValidTarget() => strikeTarget != null && !strikeTarget.dead &&
        strikeTarget.isActiveAndEnabled && strikeCollider != null && strikeCollider.enabled &&
        strikeCollider.gameObject.activeInHierarchy && !strikeCollider.isTrigger &&
        strikeCollider.GetComponentInParent<PlayerHealth>() == strikeTarget;
    private bool IsInReach(out Vector3 point) {
        point = strikeCollider != null ? strikeCollider.ClosestPoint(StrikeOrigin) : StrikeOrigin;
        return (point - StrikeOrigin).sqrMagnitude <= strikeReach * strikeReach;
    }
    private bool IsBlocked() {
        Vector3 ray = strikeCollider.bounds.center - StrikeOrigin;
        if (ray.sqrMagnitude < 0.0001f) return false;
        foreach (RaycastHit hit in Physics.RaycastAll(StrikeOrigin, ray.normalized, ray.magnitude,
            strikeBlockingLayers, QueryTriggerInteraction.Ignore)) {
            if (hit.transform.IsChildOf(transform) || hit.transform.IsChildOf(strikeTarget.transform)) continue;
            return true;
        }
        return false;
    }

    protected override void OnAbilityCancelled() => ResetStrike(AbilityState == SpecialAbilityState.Dead);
    private void ResetStrike(bool died) {
        SetIndicator(false);
        if (rotationLocked && agent != null) agent.updateRotation = restoreRotation;
        rotationLocked = false;
        if (hasAttackTrigger && animator != null) animator.ResetTrigger(attackTrigger);
        strikeTarget = null;
        strikeCollider = null;
        remainingPhaseTime = 0f;
        nextStrikeTime = Time.time + cooldownSeconds;
        Phase = died ? StrikePhase.Dead : StrikePhase.Ready;
    }
    public override void Die() {
        if (dead) return;
        Phase = StrikePhase.Dead;
        base.Die();
    }
    private void SetIndicator(bool visible) {
        if (windupIndicator == null) return;
        if (windupIndicator == gameObject || !windupIndicator.transform.IsChildOf(transform)) {
            Debug.LogError("DealerZombie: Windup Indicator는 본체가 아닌 자식 오브젝트여야 합니다.", this);
            return;
        }
        windupIndicator.SetActive(visible);
    }
    private void OnValidate() {
        windupSeconds = Mathf.Max(0.01f, windupSeconds);
        recoverySeconds = Mathf.Max(0.01f, recoverySeconds);
        cooldownSeconds = Mathf.Max(0f, cooldownSeconds);
        strikeDamageMultiplier = Mathf.Max(1f, strikeDamageMultiplier);
        strikeReach = Mathf.Max(0.01f, strikeReach);
        strikeHalfAngle = Mathf.Clamp(strikeHalfAngle, 1f, 180f);
        strikeHeight = Mathf.Max(0f, strikeHeight);
    }
}
