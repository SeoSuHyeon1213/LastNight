using System.Collections.Generic;
using UnityEngine;

// 플레이어에게 접근하면 자폭하며, 조기 처치되어도 같은 사망 폭발을 실행한다.
[DisallowMultipleComponent]
public sealed class BombZombie : SpecialZombie {
    [Header("Contact Detonation")]
    [Tooltip("폭발 중심에서 플레이어 몸 Collider까지의 거리. 폭발 반경보다 작게 설정합니다.")]
    [SerializeField, Min(0.01f)] private float detonationDistance = 1.2f;

    [Header("Death Explosion")]
    [SerializeField, Min(0.01f)] private float explosionRadius = 3f;
    [SerializeField, Min(0f)] private float explosionDamage = 50f;
    [SerializeField] private Vector3 explosionOffset = new Vector3(0f, 0.5f, 0f);
    [Tooltip("플레이어와 좀비의 몸 Collider 레이어를 포함합니다.")]
    [SerializeField] private LayerMask damageLayers = Physics.DefaultRaycastLayers;
    [Tooltip("벽·지형 등 폭발을 차단하는 레이어를 포함합니다. 생명체의 몸은 차단하지 않습니다.")]
    [SerializeField] private LayerMask blockingLayers = Physics.DefaultRaycastLayers;

    [Header("Optional Explosion Effect")]
    [SerializeField] private GameObject explosionEffectPrefab;
    [SerializeField, Min(0.1f)] private float explosionEffectLifetime = 5f;
    [SerializeField] private AudioClip explosionSound;
    [SerializeField, Range(0f, 1f)] private float explosionVolume = 1f;

    private bool deathInProgress;
    public bool HasExploded { get; private set; }

    protected override bool CanBeginAbility {
        get {
            if (!(ChaseTarget is PlayerHealth player) || CurrentAttackGoal() != player) return false;
            Collider body = player.GetComponent<Collider>();
            if (body == null || !body.enabled || body.isTrigger) return false;
            Vector3 origin = transform.position + explosionOffset;
            Vector3 point = body.ClosestPoint(origin);
            float distance = Mathf.Min(detonationDistance, explosionRadius);
            return (point - origin).sqrMagnitude <= distance * distance && !IsBlocked(origin, point);
        }
    }

    protected override void OnAbilityStarted() {
        health = 0f;
        Die();
    }
    protected override void TickAbility(float deltaTime) { }
    protected override void OnAbilityCancelled() { }

    protected override void LateUpdate() {
        base.LateUpdate();
        TryUseAbility();
    }

    protected override void BeginMeleeAttack(LivingEntity attackTarget, Collider other) {
        // 플레이어에게는 일반 근접 피해를 주지 않고 설정한 거리까지 접근한다.
        if (attackTarget is PlayerHealth) TryUseAbility();
        else base.BeginMeleeAttack(attackTarget, other);
    }

    protected override void OnAbilityReset() {
        deathInProgress = false;
        HasExploded = false;
        if (damageLayers.value == 0)
            Debug.LogError("BombZombie: Damage Layers에 플레이어·좀비 몸 레이어를 지정하세요.", this);
    }

    public override void OnDamage(float damage, Vector3 hitPoint, Vector3 hitNormal) {
        // 연쇄 폭발이 기본 사망 처리 전에 이 개체로 되돌아올 수 있다.
        if (dead || deathInProgress) return;
        base.OnDamage(damage, hitPoint, hitNormal);
    }

    public override void Die() {
        if (dead || deathInProgress) return;
        deathInProgress = true;
        try {
            if (isActiveAndEnabled &&
                (GameManager.instance == null || !GameManager.instance.isGameover)) {
                Vector3 origin = transform.position + explosionOffset;
                // 연쇄 폭발이나 사망 콜백이 Collider를 제거하기 전에 판정을 확정한다.
                List<ExplosionHit> hits = CollectHits(origin);
                HasExploded = true;
                RecordDeathAbilityExecution();
                PlayExplosionEffect(origin);
                foreach (ExplosionHit hit in hits) {
                    if (hit.Target == null || hit.Target.dead || !hit.Target.isActiveAndEnabled) continue;
                    hit.Target.OnDamage(explosionDamage, hit.Point, hit.Normal);
                }
            }
        } finally {
            // 피해 처리가 끝난 뒤 적 등록 해제·점수·웨이브 사망 통지를 한 번 처리한다.
            base.Die();
        }
    }

    private List<ExplosionHit> CollectHits(Vector3 origin) {
        var hits = new List<ExplosionHit>();
        var included = new HashSet<LivingEntity>();
        foreach (Collider candidate in Physics.OverlapSphere(origin, explosionRadius,
            damageLayers, QueryTriggerInteraction.Ignore)) {
            LivingEntity target = candidate.GetComponentInParent<LivingEntity>();
            if (target == null || target == this || target.dead || !target.isActiveAndEnabled ||
                (!(target is Zombie) && !(target is PlayerHealth)) || included.Contains(target)) continue;
            Vector3 point = candidate.ClosestPoint(origin);
            if (IsBlocked(origin, point)) continue;
            included.Add(target);
            Vector3 normal = origin - point;
            hits.Add(new ExplosionHit(target, point,
                normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector3.up));
        }
        return hits;
    }

    private bool IsBlocked(Vector3 origin, Vector3 point) {
        Vector3 delta = point - origin;
        if (delta.sqrMagnitude < 0.0001f) return false;
        foreach (RaycastHit hit in Physics.RaycastAll(origin, delta.normalized, delta.magnitude,
            blockingLayers, QueryTriggerInteraction.Ignore)) {
            LivingEntity entity = hit.collider.GetComponentInParent<LivingEntity>();
            if (hit.transform.IsChildOf(transform) || entity is Zombie || entity is PlayerHealth) continue;
            return true;
        }
        return false;
    }

    private void PlayExplosionEffect(Vector3 origin) {
        if (explosionEffectPrefab != null) {
            GameObject effect = Instantiate(explosionEffectPrefab, origin, Quaternion.identity);
            ParticleSystem[] systems = effect.GetComponentsInChildren<ParticleSystem>(true);
            foreach (ParticleSystem particles in systems) {
                // 폭발은 일회성이다. 자동 재생을 초기화하고 반복 프리팹도 한 번만 재생한다.
                particles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = particles.main;
                main.loop = false;
                main.prewarm = false;
            }
            effect.SetActive(true);
            foreach (ParticleSystem particles in systems)
                if (particles.gameObject.activeInHierarchy) particles.Play(false);
            Destroy(effect, explosionEffectLifetime);
        }
        if (explosionSound != null)
            AudioSource.PlayClipAtPoint(explosionSound, origin, explosionVolume);
    }

    protected override void OnDrawGizmosSelected() {
        base.OnDrawGizmosSelected();
        Gizmos.color = new Color(1f, 0.35f, 0f, 0.7f);
        Gizmos.DrawWireSphere(transform.position + explosionOffset, explosionRadius);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position + explosionOffset, Mathf.Min(detonationDistance, explosionRadius));
    }

    private void OnValidate() {
        explosionRadius = Mathf.Max(0.01f, explosionRadius);
        detonationDistance = Mathf.Clamp(detonationDistance, 0.01f, explosionRadius);
        explosionDamage = Mathf.Max(0f, explosionDamage);
        explosionEffectLifetime = Mathf.Max(0.1f, explosionEffectLifetime);
    }

    private readonly struct ExplosionHit {
        public readonly LivingEntity Target;
        public readonly Vector3 Point;
        public readonly Vector3 Normal;
        public ExplosionHit(LivingEntity target, Vector3 point, Vector3 normal) {
            Target = target;
            Point = point;
            Normal = normal;
        }
    }
}
