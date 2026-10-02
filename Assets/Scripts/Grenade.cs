using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public sealed class Grenade : MonoBehaviour, IThrowable {
    [Header("Throw")]
    [SerializeField] private ThrowMode throwMode = ThrowMode.Arc;
    [SerializeField, Min(0f)] private float throwPower = 12f;
    [SerializeField, Range(0f, 80f)] private float arcAngle = 30f;

    [Header("Explosion")]
    [SerializeField, Min(0.01f)] private float fuseSeconds = 3f;
    [SerializeField, Min(0.01f)] private float explosionRadius = 4f;
    [SerializeField, Min(0f)] private float damage = 100f;
    [SerializeField] private LayerMask damageLayers = Physics.DefaultRaycastLayers;
    [SerializeField] private LayerMask blockingLayers = Physics.DefaultRaycastLayers;
    [SerializeField] private bool damageOwner;

    [Header("Explosion Effect")]
    [SerializeField] private GameObject explosionEffectPrefab;
    [SerializeField, Min(0.1f)] private float explosionEffectLifetime = 5f;

    private Rigidbody body;
    private GameObject thrower;
    private Collider[] grenadeColliders;
    private readonly HashSet<IDamageable> damagedTargets = new HashSet<IDamageable>();
    private bool thrown;
    private bool exploded;
    private float remainingFuse;

    public bool HasBeenThrown => thrown;

    private void Awake() {
        body = GetComponent<Rigidbody>();
        grenadeColliders = GetComponentsInChildren<Collider>();
        body.isKinematic = true;
        body.useGravity = false;
    }

    public void SetThrowMode(ThrowMode mode) {
        if (!thrown) throwMode = mode;
    }

    public void SetThrowPower(float speed) {
        if (!thrown && !float.IsNaN(speed) && !float.IsInfinity(speed))
            throwPower = Mathf.Max(0f, speed);
    }

    public void Throw(Vector3 direction, GameObject owner) {
        if (thrown || exploded || !isActiveAndEnabled) return;
        if (direction.sqrMagnitude < 0.0001f ||
            float.IsNaN(direction.sqrMagnitude) || float.IsInfinity(direction.sqrMagnitude)) {
            Debug.LogError("Grenade: 유효한 투척 방향이 필요합니다.", this);
            return;
        }
        bool hasCollider = false;
        foreach (Collider collider in grenadeColliders)
            if (collider != null && collider.enabled && !collider.isTrigger) hasCollider = true;
        if (!hasCollider) {
            Debug.LogError("Grenade: 활성화된 비 Trigger Collider가 필요합니다.", this);
            return;
        }

        thrower = owner;
        if (owner != null) {
            foreach (Collider ownCollider in owner.GetComponentsInChildren<Collider>(true))
                foreach (Collider grenadeCollider in grenadeColliders)
                    if (ownCollider != null && grenadeCollider != null && !ownCollider.transform.IsChildOf(transform))
                        Physics.IgnoreCollision(grenadeCollider, ownCollider);
        }
        transform.SetParent(null, true);
        Vector3 launchDirection = direction.normalized;
        if (throwMode == ThrowMode.Arc) {
            Vector3 horizontal = Vector3.ProjectOnPlane(launchDirection, Vector3.up);
            if (horizontal.sqrMagnitude > 0.0001f) {
                float angle = arcAngle * Mathf.Deg2Rad;
                launchDirection = horizontal.normalized * Mathf.Cos(angle) + Vector3.up * Mathf.Sin(angle);
            }
        }
        thrown = true;
        remainingFuse = fuseSeconds;
        body.isKinematic = false;
        body.useGravity = throwMode == ThrowMode.Arc;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.linearVelocity = launchDirection * throwPower;
        body.angularVelocity = Vector3.zero;
    }

    private void Update() {
        if (!thrown || exploded) return;
        remainingFuse -= Time.deltaTime;
        if (remainingFuse <= 0f) Explode();
    }

    private void Explode() {
        if (exploded) return;
        exploded = true;
        Vector3 origin = body.worldCenterOfMass;
        damagedTargets.Clear();
        foreach (Collider candidate in Physics.OverlapSphere(origin, explosionRadius,
            damageLayers, QueryTriggerInteraction.Ignore)) {
            if (candidate.transform.IsChildOf(transform)) continue;
            if (!damageOwner && thrower != null && candidate.transform.IsChildOf(thrower.transform)) continue;
            IDamageable target = candidate.GetComponentInParent<IDamageable>();
            if (target == null || damagedTargets.Contains(target)) continue;
            Vector3 point = candidate.ClosestPoint(origin);
            if (IsBlocked(origin, point, target)) continue;
            damagedTargets.Add(target);
            Vector3 normal = origin - point;
            target.OnDamage(damage, point, normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector3.up);
        }
        if (explosionEffectPrefab != null) {
            // 수류탄과 별도로 생성해 본체 제거 후에도 폭발 이펙트를 유지한다.
            GameObject effect = Instantiate(explosionEffectPrefab, origin, Quaternion.identity);
            effect.SetActive(true);
            foreach (ParticleSystem particles in effect.GetComponentsInChildren<ParticleSystem>())
                particles.Play(false);
            Destroy(effect, explosionEffectLifetime);
        }
        Destroy(gameObject);
    }

    private bool IsBlocked(Vector3 origin, Vector3 point, IDamageable target) {
        Vector3 delta = point - origin;
        if (delta.sqrMagnitude < 0.0001f) return false;
        foreach (RaycastHit hit in Physics.RaycastAll(origin, delta.normalized, delta.magnitude,
            blockingLayers, QueryTriggerInteraction.Ignore)) {
            if (hit.transform.IsChildOf(transform)) continue;
            if (hit.collider.GetComponentInParent<IDamageable>() == target) continue;
            // 자기 피해를 끈 경우 투척자 몸이 폭발 판정을 가리지 않게 한다.
            if (!damageOwner && thrower != null && hit.transform.IsChildOf(thrower.transform)) continue;
            return true;
        }
        return false;
    }

    private void OnValidate() {
        throwPower = Mathf.Max(0f, throwPower);
        arcAngle = Mathf.Clamp(arcAngle, 0f, 80f);
        fuseSeconds = Mathf.Max(0.01f, fuseSeconds);
        explosionRadius = Mathf.Max(0.01f, explosionRadius);
        damage = Mathf.Max(0f, damage);
        explosionEffectLifetime = Mathf.Max(0.1f, explosionEffectLifetime);
    }
}
