using System.Collections.Generic;
using UnityEngine;

public sealed class MeleeWeapon : Gun {
    [Header("Melee Attack")]
    [SerializeField] private SphereCollider hitCollider;
    [SerializeField, Min(0.1f)] private float animationTimeout = 3f;
    [SerializeField] private Vector3 originOffset = new Vector3(0f, 1f, 0f);
    [SerializeField] private LayerMask damageLayers = Physics.DefaultRaycastLayers;
    [SerializeField] private LayerMask blockingLayers = Physics.DefaultRaycastLayers;
    private PlayerHealth owner;
    private Animator ownerAnimator;
    private AudioSource audioPlayer;
    private readonly HashSet<Zombie> damagedTargets = new HashSet<Zombie>();
    private static readonly int AttackId = Animator.StringToHash("MeleeAttack");
    private bool attacking;
    private bool hitWindowOpen;
    private bool hasPreviousCenter;
    private Vector3 previousCenter;
    private float attackStartedAt;
    private float nextAttackTime;

    public override bool UsesAmmo => false;
    public bool IsAttacking => attacking;

    protected override void Awake() {
        base.Awake();
        owner = GetComponentInParent<PlayerHealth>();
        ownerAnimator = owner != null ? owner.GetComponent<Animator>() : null;
        audioPlayer = GetComponent<AudioSource>();
        if (hitCollider == null) hitCollider = GetComponent<SphereCollider>();
        if (hitCollider == null || hitCollider.gameObject != gameObject || !hitCollider.isTrigger) {
            Debug.LogError("MeleeWeapon: 무기 루트에 Is Trigger가 켜진 SphereCollider를 연결하세요.", this);
            enabled = false;
            return;
        }
        Rigidbody body = GetComponent<Rigidbody>();
        if (body == null || !body.isKinematic || body.useGravity) {
            Debug.LogError("MeleeWeapon: 무기 루트에 Use Gravity가 꺼진 Kinematic Rigidbody가 필요합니다.", this);
            enabled = false;
            return;
        }
        hitCollider.enabled = false;
    }

    public override void Fire() {
        if (!CanAttack() || ownerAnimator == null || attacking || Time.time < nextAttackTime) return;
        attacking = true;
        attackStartedAt = Time.time;
        damagedTargets.Clear();
        nextAttackTime = Time.time + Mathf.Max(0.01f, gunData.timeBetFire);
        ownerAnimator.ResetTrigger(AttackId);
        ownerAnimator.SetTrigger(AttackId);
        if (audioPlayer != null && gunData.shotClip != null) audioPlayer.PlayOneShot(gunData.shotClip);
    }

    private bool CanAttack() {
        GameSessionManager session = GameSessionManager.instance;
        return isActiveAndEnabled && owner != null && !owner.dead && gunData != null &&
            Time.timeScale > 0f && (GameManager.instance == null || !GameManager.instance.isGameover) &&
            (session == null || (!session.IsPaused && !session.IsRebinding));
    }

    private void Update() {
        if (!attacking) return;
        if (owner == null || owner.dead || (GameManager.instance != null && GameManager.instance.isGameover)) {
            FinishAttack();
            return;
        }
        if (Time.time - attackStartedAt > animationTimeout) {
            Debug.LogWarning("MeleeWeapon: 공격 애니메이션 종료를 받지 못했습니다. melee 상태의 MeleeAttackAnimation을 확인하세요.", this);
            FinishAttack();
        }
    }

    public void SetHitWindow(bool open) {
        open &= attacking && CanAttack();
        if (open == hitWindowOpen) return;
        hitWindowOpen = open;
        hasPreviousCenter = false;
        if (hitCollider != null) hitCollider.enabled = open;
    }

    public void FinishAttack() {
        attacking = false;
        SetHitWindow(false);
        damagedTargets.Clear();
        if (ownerAnimator != null) ownerAnimator.ResetTrigger(AttackId);
    }

    // 손의 최종 애니메이션 위치에 배트를 배치한 뒤 PlayerShooter가 호출한다.
    public void SampleHitbox() {
        if (!hitWindowOpen || !CanAttack() || hitCollider == null) return;
        Vector3 center = hitCollider.transform.TransformPoint(hitCollider.center);
        Vector3 scale = hitCollider.transform.lossyScale;
        float radius = hitCollider.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        foreach (Collider other in Physics.OverlapSphere(center, radius, damageLayers, QueryTriggerInteraction.Ignore))
            TryDamage(other, center);
        // 프레임 사이에 이동한 타격구도 검사해 빠른 스윙이 적을 건너뛰지 않게 한다.
        Vector3 travel = center - previousCenter;
        if (hasPreviousCenter && travel.sqrMagnitude > 0.000001f) {
            foreach (RaycastHit hit in Physics.SphereCastAll(previousCenter, radius, travel.normalized,
                travel.magnitude, damageLayers, QueryTriggerInteraction.Ignore)) TryDamage(hit.collider, hit.point);
        }
        previousCenter = center;
        hasPreviousCenter = true;
    }

    private void OnTriggerEnter(Collider other) {
        if (hitCollider != null) TryDamage(other, transform.TransformPoint(hitCollider.center));
    }
    private void OnTriggerStay(Collider other) {
        if (hitCollider != null) TryDamage(other, transform.TransformPoint(hitCollider.center));
    }

    private void TryDamage(Collider other, Vector3 contactOrigin) {
        if (!hitWindowOpen || !CanAttack() || other == null ||
            (damageLayers.value & (1 << other.gameObject.layer)) == 0 || other.transform.IsChildOf(owner.transform)) return;
        Zombie target = other.GetComponentInParent<Zombie>();
        if (target == null || target.dead || damagedTargets.Contains(target)) return;
        Vector3 point = other.ClosestPoint(contactOrigin);
        Vector3 origin = owner.transform.TransformPoint(originOffset);
        if (IsBlocked(origin, point, target)) return;
        damagedTargets.Add(target);
        Vector3 delta = point - contactOrigin;
        target.OnDamage(Mathf.Max(0f, gunData.damage), point,
            delta.sqrMagnitude > 0.0001f ? -delta.normalized : -owner.transform.forward);
        if (muzzleFlashEffect != null) muzzleFlashEffect.Play();
    }

    private bool IsBlocked(Vector3 origin, Vector3 point, Zombie target) {
        Vector3 delta = point - origin;
        if (delta.sqrMagnitude < 0.0001f) return false;
        foreach (RaycastHit hit in Physics.RaycastAll(origin, delta.normalized, delta.magnitude,
            blockingLayers, QueryTriggerInteraction.Ignore)) {
            if (hit.transform.IsChildOf(owner.transform)) continue;
            if (hit.collider.GetComponentInParent<Zombie>() == target) continue;
            return true;
        }
        return false;
    }

    public override bool Reload() => false;
    public override void AddAmmo(int amount) { }

    protected override void OnDisable() {
        FinishAttack();
        base.OnDisable();
    }

    private void OnValidate() { animationTimeout = Mathf.Max(0.1f, animationTimeout); }
}
