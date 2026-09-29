using System.Collections.Generic;
using UnityEngine;

public sealed class Shotgun : Gun {
    [SerializeField, Range(1, 32)] private int pelletCount = 8;
    [SerializeField, Range(0f, 45f), Tooltip("총구 정면에서 산탄이 퍼지는 최대 각도(반각).")]
    private float spreadAngle = 6f;
    [SerializeField, Min(0f)] private float damageFalloffStart = 8f;
    [SerializeField, Min(0.1f)] private float damageFalloffEnd = 25f;
    [SerializeField, Range(0f, 1f)] private float minimumDamageMultiplier = 0.2f;

    private struct PelletDamage {
        public float amount;
        public RaycastHit hit;
    }

    private readonly Dictionary<IDamageable, PelletDamage> damageByTarget =
        new Dictionary<IDamageable, PelletDamage>();

    protected override Vector3 ResolveShot(bool hasHit, RaycastHit firstHit, Vector3 hitPosition) {
        damageByTarget.Clear();
        Vector3 origin = fireTransform.position;
        Vector3 effectEnd = origin + fireTransform.forward * FireDistance;
        float coneRadius = Mathf.Tan(spreadAngle * Mathf.Deg2Rad);

        for (int i = 0; i < pelletCount; i++) {
            Vector2 spread = Random.insideUnitCircle * coneRadius;
            Vector3 direction = (fireTransform.forward + fireTransform.right * spread.x +
                fireTransform.up * spread.y).normalized;
            bool pelletHit = TryGetDirectionalShotHit(direction, out RaycastHit hit);
            // 기본 Gun의 단일 궤적은 첫 번째 산탄을 표시한다.
            if (i == 0) effectEnd = pelletHit ? hit.point : origin + direction * FireDistance;
            if (!pelletHit) continue;

            IDamageable target = hit.collider.GetComponentInParent<IDamageable>();
            if (target == null) continue;
            float falloff = Mathf.InverseLerp(damageFalloffStart, damageFalloffEnd, hit.distance);
            // GunData.damage는 산탄 한 개의 근거리 피해다.
            float damage = Mathf.Max(0f, gunData.damage) *
                Mathf.Lerp(1f, minimumDamageMultiplier, falloff);
            if (damage <= 0f) continue;
            damageByTarget.TryGetValue(target, out PelletDamage accumulated);
            accumulated.amount += damage;
            accumulated.hit = hit;
            damageByTarget[target] = accumulated;
        }

        // 모든 충돌을 먼저 수집해 사망 시 콜라이더 해제가 뒤쪽 적의 피격에 영향을 주지 않게 한다.
        foreach (KeyValuePair<IDamageable, PelletDamage> pair in damageByTarget) {
            if (pair.Key is Object targetObject && targetObject == null) continue;
            pair.Key.OnDamage(pair.Value.amount, pair.Value.hit.point, pair.Value.hit.normal);
        }
        damageByTarget.Clear();
        return effectEnd;
    }

    private void OnValidate() {
        pelletCount = Mathf.Clamp(pelletCount, 1, 32);
        spreadAngle = Mathf.Clamp(spreadAngle, 0f, 45f);
        damageFalloffStart = Mathf.Max(0f, damageFalloffStart);
        damageFalloffEnd = Mathf.Max(damageFalloffStart + 0.1f, damageFalloffEnd);
        minimumDamageMultiplier = Mathf.Clamp01(minimumDamageMultiplier);
    }
}
