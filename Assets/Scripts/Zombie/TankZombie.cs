using System;
using UnityEngine;

// 높은 HP와 느린 이동은 전용 ZombieData로 설정한다. 장갑·약점은 상시 방어 특성이다.
[DisallowMultipleComponent]
public sealed class TankZombie : SpecialZombie {
    [Header("Passive Armor")]
    [Tooltip("약점 밖에서 받는 피해 비율입니다. 0.5는 피해 50%를 받습니다.")]
    [SerializeField, Range(0f, 1f)] private float armorDamageMultiplier = 0.5f;
    [Tooltip("약점에서 받는 피해 비율입니다. 1은 원래 피해를 그대로 받습니다.")]
    [SerializeField, Min(1f)] private float weakPointDamageMultiplier = 1f;

    [Header("Weak Point Regions")]
    [Tooltip("머리·등 등 노출 부위에 배치한 자식 Transform과 월드 반경입니다. 몸 Collider 표면의 피격 위치가 영역 안에 있어야 합니다.")]
    [SerializeField] private WeakPointRegion[] weakPoints = Array.Empty<WeakPointRegion>();

    // 장갑은 능력 실행 횟수나 능력 사용 전 처치 도전의 실행 이벤트로 집계하지 않는다.
    protected override bool CanBeginAbility => false;
    protected override void OnAbilityStarted() { }
    protected override void TickAbility(float deltaTime) { }
    protected override void OnAbilityCancelled() { }

    protected override void OnAbilityReset() {
        if (!HasValidWeakPoint())
            Debug.LogError("TankZombie: 유효한 Weak Points가 없습니다. 자식 Center와 양수 Radius를 연결하세요. 연결 전에는 장갑 없이 정상 피해를 받습니다.", this);
    }

    public override void OnDamage(float damage, Vector3 hitPoint, Vector3 hitNormal) {
        if (dead || !isActiveAndEnabled || !IsFinite(damage) || damage <= 0f ||
            !IsFinite(hitPoint.x) || !IsFinite(hitPoint.y) || !IsFinite(hitPoint.z)) return;
        float multiplier = GetDamageMultiplier(hitPoint);
        // 최종 피해만 정수화하며 기존 LivingEntity가 사망 통지 시 음수 HP를 보이지 않게 한다.
        float actualDamage = Mathf.Min(Mathf.Max(0f, health), Mathf.Round(damage * multiplier));
        if (actualDamage <= 0f) return;
        Vector3 normal = hitNormal;
        if (!IsFinite(normal.sqrMagnitude) || normal.sqrMagnitude < 0.0001f)
            normal = Vector3.up;
        base.OnDamage(actualDamage, hitPoint, normal);
    }

    public bool IsWeakPointHit(Vector3 hitPoint) {
        if (weakPoints == null) return false;
        foreach (WeakPointRegion region in weakPoints) {
            if (!IsValid(region)) continue;
            if ((hitPoint - region.Center.position).sqrMagnitude <= region.Radius * region.Radius)
                return true;
        }
        return false;
    }

    private float GetDamageMultiplier(Vector3 hitPoint) {
        if (IsWeakPointHit(hitPoint))
            return IsFinite(weakPointDamageMultiplier) ? Mathf.Max(1f, weakPointDamageMultiplier) : 1f;
        // 설정 누락으로 약점 없는 장갑 적이 되지 않도록 방어를 적용하지 않는다.
        if (!HasValidWeakPoint()) return 1f;
        return IsFinite(armorDamageMultiplier) ? Mathf.Clamp01(armorDamageMultiplier) : 1f;
    }

    private bool HasValidWeakPoint() {
        if (weakPoints == null) return false;
        foreach (WeakPointRegion region in weakPoints)
            if (IsValid(region)) return true;
        return false;
    }

    private bool IsValid(WeakPointRegion region) {
        return region != null && region.Center != null && region.Center != transform &&
            region.Center.IsChildOf(transform) && region.Center.gameObject.activeInHierarchy &&
            IsFinite(region.Radius) && region.Radius > 0f;
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    protected override void OnDrawGizmosSelected() {
        base.OnDrawGizmosSelected();
        if (weakPoints == null) return;
        Gizmos.color = Color.yellow;
        foreach (WeakPointRegion region in weakPoints) {
            if (!IsValid(region)) continue;
            Gizmos.DrawWireSphere(region.Center.position, region.Radius);
        }
    }

    private void OnValidate() {
        armorDamageMultiplier = IsFinite(armorDamageMultiplier) ? Mathf.Clamp01(armorDamageMultiplier) : 0.5f;
        weakPointDamageMultiplier = IsFinite(weakPointDamageMultiplier) ? Mathf.Max(1f, weakPointDamageMultiplier) : 1f;
        if (weakPoints == null) return;
        foreach (WeakPointRegion region in weakPoints)
            if (region != null) region.Validate();
    }

    [Serializable]
    private sealed class WeakPointRegion {
        [SerializeField] private Transform center;
        [SerializeField, Min(0.01f)] private float radius = 0.25f;
        public Transform Center => center;
        public float Radius => radius;
        public void Validate() { radius = IsFinite(radius) ? Mathf.Max(0.01f, radius) : 0.25f; }
    }
}
