using System.Collections.Generic;
using UnityEngine;

public sealed class Sniper : Gun {
    [SerializeField, Range(0f, 1f)] private float initialPenetrationChance = 0.8f;
    [SerializeField, Range(0f, 1f)] private float chanceDecreasePerPenetration = 0.1f;
    [SerializeField, Range(0f, 1f)] private float damageLossPerPenetration = 0.1f;
    private const int MaxPenetrations = 2;
    private readonly HashSet<Zombie> damagedZombies = new HashSet<Zombie>();

    protected override Vector3 ResolveShot(bool hasHit, RaycastHit firstHit, Vector3 hitPosition) {
        Vector3 end = fireTransform.position + fireTransform.forward * FireDistance;
        if (!hasHit) return end;
        RaycastHit[] hits = GetOrderedShotHits();
        damagedZombies.Clear();
        int penetrations = 0;
        float damage = gunData.damage;
        float chance = initialPenetrationChance;

        foreach (RaycastHit hit in hits) {
            if (hit.collider == null) continue;
            Zombie zombie = hit.collider.GetComponentInParent<Zombie>();
            if (zombie == null) {
                // 좀비가 아닌 대상은 피해를 받을 수 있어도 관통하지 않는다.
                IDamageable target = hit.collider.GetComponentInParent<IDamageable>();
                if (target != null) target.OnDamage(damage, hit.point, hit.normal);
                return hit.point;
            }
            if (!damagedZombies.Add(zombie) || zombie.dead) continue;
            zombie.OnDamage(damage, hit.point, hit.normal);
            // 실패한 대상에도 피해를 준 뒤 탄을 멈춘다. 한 좀비에는 한 번만 판정한다.
            if (penetrations >= MaxPenetrations || Random.value >= chance)
                return hit.point;
            penetrations++;
            damage *= 1f - damageLossPerPenetration;
            chance = Mathf.Clamp01(chance - chanceDecreasePerPenetration);
        }
        return end;
    }
}
