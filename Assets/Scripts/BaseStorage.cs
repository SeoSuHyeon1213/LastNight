using System;
using UnityEngine;
using UnityEngine.AI;

// 거점 보관소 목표물. Wave 역할 좀비가 대상이 없을 때 이동해 공격한다.
// 좀비 피해·파괴 패배와 에디터에 연결된 진입 경로를 관리한다.
public sealed class BaseStorage : LivingEntity {
    [Tooltip("좀비가 다가가 공격할 콜라이더. 비워 두면 같은 오브젝트의 Collider를 사용한다.")]
    [SerializeField] private Collider attackCollider;
    [SerializeField] private GameManager gameManager;
    [Tooltip("테스트 시작값. 확정 밸런스가 아니다.")]
    [SerializeField, Min(1f)] private float maximumHealth = 1000f;
    [SerializeField] private BaseEntryRoute[] entryRoutes;
    [SerializeField, Min(0.05f)] private float pathSnapDistance = 0.75f;
    private NavMeshPath path;
    private NavMeshPath insidePath;

    public Collider AttackCollider => attackCollider;
    public float MaximumHealth => maximumHealth;
    public int HitCount { get; private set; }
    public float TotalDamageTaken { get; private set; }
    public event Action<float> Damaged;

    private void Awake() {
        if (attackCollider == null) attackCollider = GetComponent<Collider>();
        if (attackCollider == null || gameManager == null) {
            Debug.LogError("BaseStorage: Attack Collider와 Game Manager를 연결하세요.", this);
            enabled = false;
        }
    }

    protected override void OnEnable() {
        base.OnEnable();
        health = maximumHealth;
        HitCount = 0;
        TotalDamageTaken = 0f;
    }

    // 기존 IDamageable 호출(플레이어 총알·수류탄)은 보관소를 손상시키지 않는다.
    public override void OnDamage(float damage, Vector3 hitPoint, Vector3 hitNormal) { }

    public void ReceiveZombieDamage(Zombie attacker, float damage, Vector3 hitPoint, Vector3 hitNormal) {
        if (attacker == null || attacker.dead || !attacker.isActiveAndEnabled || !isActiveAndEnabled ||
            dead || damage <= 0f || float.IsNaN(damage) || float.IsInfinity(damage) ||
            Time.timeScale <= 0f || gameManager == null || gameManager.isGameover) return;
        float applied = Mathf.Min(health, damage);
        health = Mathf.Max(0f, health - applied);
        HitCount++;
        TotalDamageTaken += applied;
        // 피해 이벤트의 다른 리스너보다 패배를 먼저 확정한다.
        if (health <= 0f) Die();
        Damaged?.Invoke(applied);
    }

    // 거점 전환: 같은 보관소를 새 집으로 옮겨 HP·피해 기록을 유지한다(세션 거점 내구도 공유).
    // 좀비는 같은 참조를 유지하고 다음 경로 갱신에서 새 위치·진입로를 사용한다. 진행 중 공격은 타격 시 거리 재검사로 무효가 된다.
    public void Relocate(Transform anchor, BaseEntryRoute[] routes) {
        if (anchor == null) return;
        transform.SetPositionAndRotation(anchor.position, anchor.rotation);
        entryRoutes = routes;
        Physics.SyncTransforms();
    }

    public override void RestoreHealth(float amount) {
        if (!dead && amount > 0f && !float.IsNaN(amount) && !float.IsInfinity(amount))
            health = Mathf.Min(maximumHealth, health + amount);
    }

    public override void Die() {
        if (dead) return;
        health = 0f;
        if (gameManager != null) gameManager.EndGame();
        base.Die();
    }

    // 열린 완전 경로를 우선한다. 없으면 도달 가능한 바리케이드 외부 지점으로 이동한다.
    // 생성 검증도 '바리케이드 파괴 후 진입 가능'을 도달 가능으로 인정한다.
    public bool TryGetApproach(Vector3 origin, int agentTypeId, int areaMask, float agentRadius,
        out Vector3 destination, out Barricade blocker) {
        destination = default;
        blocker = null;
        if (dead || !isActiveAndEnabled || attackCollider == null) return false;
        if (path == null) path = new NavMeshPath();
        if (insidePath == null) insidePath = new NavMeshPath();
        var filter = new NavMeshQueryFilter { agentTypeID = agentTypeId, areaMask = areaMask };
        Vector3 surface = attackCollider.ClosestPoint(origin);
        Vector3 toward = origin - surface;
        toward.y = 0f;
        Vector3 approach = surface + (toward.sqrMagnitude > 0.0001f ? toward.normalized : Vector3.forward) * (agentRadius + 0.1f);
        if (!NavMesh.SamplePosition(approach, out NavMeshHit goal, pathSnapDistance, filter)) return false;
        if (CompletePath(origin, goal.position, filter, path) && !PathHasBarricade(path)) {
            destination = goal.position;
            return true;
        }
        float bestOpen = float.PositiveInfinity;
        float bestBlocked = float.PositiveInfinity;
        Vector3 blockedDestination = default;
        Barricade blockedRoute = null;
        if (entryRoutes == null) return false;
        foreach (BaseEntryRoute route in entryRoutes) {
            if (route == null || !route.isActiveAndEnabled || route.Inside == null) continue;
            if (!NavMesh.SamplePosition(route.transform.position, out NavMeshHit outside, pathSnapDistance, filter) ||
                !NavMesh.SamplePosition(route.Inside.position, out NavMeshHit inside, pathSnapDistance, filter)) continue;
            if (!CompletePath(origin, outside.position, filter, path) || PathHasBarricade(path) ||
                !CompletePath(inside.position, goal.position, filter, insidePath) || PathHasBarricade(insidePath)) continue;
            float length = PathLength(path) + PathLength(insidePath) + Vector3.Distance(outside.position, inside.position);
            bool closed = route.Barricade != null && route.Barricade.IsBlocking;
            if (closed) {
                if (length >= bestBlocked) continue;
                bestBlocked = length;
                blockedDestination = outside.position;
                blockedRoute = route.Barricade;
            } else if (length < bestOpen && CompletePath(outside.position, inside.position, filter, path) && !PathHasBarricade(path)) {
                bestOpen = length;
                destination = outside.position;
            }
        }
        if (!float.IsPositiveInfinity(bestOpen)) return true;
        if (blockedRoute == null) return false;
        blocker = blockedRoute;
        destination = blockedDestination;
        return true;
    }

    private static bool CompletePath(Vector3 from, Vector3 to, NavMeshQueryFilter filter, NavMeshPath result) =>
        NavMesh.CalculatePath(from, to, filter, result) && result.status == NavMeshPathStatus.PathComplete;

    private static float PathLength(NavMeshPath result) {
        float length = 0f;
        Vector3[] corners = result.corners;
        for (int i = 1; i < corners.Length; i++) length += Vector3.Distance(corners[i - 1], corners[i]);
        return length;
    }

    private static bool PathHasBarricade(NavMeshPath result) {
        Vector3[] corners = result.corners;
        for (int i = 1; i < corners.Length; i++) {
            Vector3 origin = corners[i - 1] + Vector3.up;
            Vector3 ray = corners[i] - corners[i - 1];
            foreach (RaycastHit hit in Physics.RaycastAll(origin, ray.normalized, ray.magnitude,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) {
                Barricade barricade = hit.collider.GetComponentInParent<Barricade>();
                if (barricade != null && barricade.IsBlocking) return true;
            }
        }
        return false;
    }
}
