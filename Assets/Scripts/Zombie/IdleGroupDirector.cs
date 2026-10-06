using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.AI;

// 대기 좀비 군집의 배치 위치 선택과 정체 방지 각성을 맡는다.
// 수량은 ZombieSpawner의 예정 일반 수 B의 일부이며, 생성·슬롯·웨이브 종료는 스포너와 EnemyRegistry 경로를 그대로 쓴다.
public sealed class IdleGroupDirector : MonoBehaviour {
    private sealed class ReferenceComparer : IEqualityComparer<Zombie> {
        public bool Equals(Zombie a, Zombie b) => ReferenceEquals(a, b);
        public int GetHashCode(Zombie obj) => RuntimeHelpers.GetHashCode(obj);
    }

    [SerializeField] private IdleGroupProfile profile;
    [Tooltip("에디터에서 배치한 군집 후보 지점")]
    [SerializeField] private IdleGroupPoint[] points;
    [SerializeField] private EnemyRegistry enemyRegistry;
    [SerializeField] private ZombieSpawner waveSpawner;
    [Tooltip("거리 기준과 각성 시 추적 대상")]
    [SerializeField] private PlayerHealth player;
    [Tooltip("각성 조건을 확인하는 간격(초)")]
    [SerializeField, Min(0.05f)] private float checkInterval = 0.5f;

    private readonly HashSet<Zombie> members = new HashSet<Zombie>(new ReferenceComparer());
    private readonly List<Zombie> buffer = new List<Zombie>();
    private readonly List<IdleGroupPoint> candidates = new List<IdleGroupPoint>();
    private bool active; // 웨이브 생성·정리 중에만 감시
    private float lastProgressTime; // 마지막 유효 처치 또는 생성 시각
    private float idleOnlySince = -1f;
    private float nextCheckTime;

    public bool IsReady => isActiveAndEnabled;
    public int DormantCount { get; private set; }

    private void Awake() {
        if (profile == null || enemyRegistry == null || waveSpawner == null || player == null) {
            Debug.LogError("IdleGroupDirector: Profile, Enemy Registry, Wave Spawner, Player를 연결하세요. 대기 군집을 쓰지 않습니다.", this);
            enabled = false;
            return;
        }
        if (points == null || points.Length == 0)
            Debug.LogWarning("IdleGroupDirector: 군집 후보 지점이 없어 대기 물량은 일반 생성으로 돌아갑니다.", this);
    }

    private void OnEnable() {
        if (enemyRegistry != null) {
            enemyRegistry.EnemyDied += HandleEnemyDied;
            enemyRegistry.EnemyRegistered += HandleEnemyRegistered;
        }
        if (waveSpawner != null) waveSpawner.PhaseChanged += HandlePhaseChanged;
        ResetMonitor();
    }

    private void OnDisable() {
        if (enemyRegistry != null) {
            enemyRegistry.EnemyDied -= HandleEnemyDied;
            enemyRegistry.EnemyRegistered -= HandleEnemyRegistered;
        }
        if (waveSpawner != null) waveSpawner.PhaseChanged -= HandlePhaseChanged;
        members.Clear();
        ResetMonitor();
        active = false;
    }

    public int GetIdleCount(int wave, int baseCount) => profile.GetIdleCount(wave, baseCount);

    // 에디터 지점 주변에서만 위치를 고른다. 1~3마리 단위로 지점마다 한 군집. 자리가 부족하면 가능한 수만 반환한다.
    public void PlanPlacements(int count, List<Vector3> output) {
        output.Clear();
        if (count <= 0 || points == null || enemyRegistry.SpawnValidator == null || !enemyRegistry.SpawnValidator.IsConfigured) return;

        Vector3 playerPosition = player.transform.position;
        candidates.Clear();
        foreach (IdleGroupPoint point in points) {
            if (point == null || !point.isActiveAndEnabled) continue;
            Vector3 offset = point.Position - playerPosition;
            offset.y = 0f;
            float distance = offset.magnitude;
            if (distance < profile.MinPlayerDistance) continue;
            if (profile.MaxPlayerDistance > 0f && distance > profile.MaxPlayerDistance) continue;
            candidates.Add(point);
        }
        // 무작위 순서로 지점을 고른다
        for (int i = candidates.Count - 1; i > 0; i--) {
            int j = Random.Range(0, i + 1);
            (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
        }

        int remaining = count;
        foreach (IdleGroupPoint point in candidates) {
            if (remaining <= 0) break;
            int size = Mathf.Min(Random.Range(profile.MinGroupSize, profile.MaxGroupSize + 1), point.Capacity, remaining);
            for (int member = 0; member < size; member++) {
                if (TryFindMemberPosition(point, output, out Vector3 position)) {
                    output.Add(position);
                    remaining--;
                }
            }
        }
        if (remaining > 0)
            Debug.Log($"IdleGroupDirector: 대기 {count}마리 중 {count - remaining}마리만 배치. 나머지는 일반 생성으로 처리합니다. " +
                $"(조건에 맞는 지점 {candidates.Count}개)", this);
    }

    private bool TryFindMemberPosition(IdleGroupPoint point, List<Vector3> placed, out Vector3 position) {
        float spacingSqr = profile.MemberSpacing * profile.MemberSpacing;
        for (int attempt = 0; attempt < profile.AttemptsPerMember; attempt++) {
            Vector2 offset = Random.insideUnitCircle * point.SpreadRadius;
            Vector3 candidate = point.Position + new Vector3(offset.x, 0f, offset.y);
            if (!enemyRegistry.SpawnValidator.TrySnap(candidate, profile.NavMeshSampleDistance,
                profile.AgentTypeId, profile.AreaMask, out Vector3 finalPosition))
                continue;
            if (!enemyRegistry.SpawnValidator.Validate(finalPosition, profile.CapsuleRadius, profile.CapsuleHeight,
                profile.BlockingLayers, player.transform.position, profile.MinPlayerDistance, profile.MaxPlayerDistance,
                profile.AgentTypeId, profile.AreaMask)) continue;
            bool tooClose = false;
            foreach (Vector3 other in placed) {
                Vector3 delta = finalPosition - other;
                delta.y = 0f;
                if (delta.sqrMagnitude < spacingSqr) { tooClose = true; break; }
            }
            if (tooClose) continue;
            position = finalPosition;
            return true;
        }
        position = default;
        return false;
    }

    // 스포너가 대기 구성원 생성에 성공하면 호출한다.
    public bool IsPlacementValid(Vector3 position) =>
        IsReady && enemyRegistry.SpawnValidator != null && enemyRegistry.SpawnValidator.Validate(position,
            profile.CapsuleRadius, profile.CapsuleHeight, profile.BlockingLayers, player.transform.position,
            profile.MinPlayerDistance, profile.MaxPlayerDistance, profile.AgentTypeId, profile.AreaMask);

    public void RegisterMember(Zombie zombie) {
        if (zombie == null) return;
        members.Add(zombie);
        zombie.MarkIdleGroupMember();
    }

    private void HandleEnemyDied(Zombie zombie) {
        members.Remove(zombie);
        lastProgressTime = Time.time;
    }

    private void HandleEnemyRegistered(Zombie zombie) {
        lastProgressTime = Time.time;
    }

    private void HandlePhaseChanged(WaveStatus status) {
        active = status.Phase == WavePhase.Spawning || status.Phase == WavePhase.Clearing;
        if (status.Phase == WavePhase.Spawning || status.Phase == WavePhase.None) ResetMonitor();
        if (status.Phase == WavePhase.None) members.Clear();
    }

    private void ResetMonitor() {
        lastProgressTime = Time.time;
        idleOnlySince = -1f;
        nextCheckTime = 0f;
        DormantCount = 0;
    }

    private void Update() {
        if (!active || Time.timeScale <= 0f ||
            (GameManager.instance != null && GameManager.instance.isGameover)) return;
        if (Time.time < nextCheckTime) return;
        nextCheckTime = Time.time + checkInterval;
        EvaluateWake();
    }

    // 예정 생성 완료 여부와 무관하게 두 조건 중 하나면 각성한다.
    private void EvaluateWake() {
        buffer.Clear();
        foreach (Zombie member in members)
            if (member != null && !member.dead && member.State == ZombieState.Idle) buffer.Add(member);
        members.RemoveWhere(member => member == null || member.dead);
        DormantCount = buffer.Count;

        if (DormantCount == 0) {
            idleOnlySince = -1f;
            return;
        }

        float now = Time.time;
        if (enemyRegistry.AliveCount == DormantCount) {
            if (idleOnlySince < 0f) idleOnlySince = now;
        } else {
            idleOnlySince = -1f;
        }

        bool stalled = now - lastProgressTime >= profile.StallWakeSeconds;
        bool idleOnly = idleOnlySince >= 0f && now - idleOnlySince >= profile.IdleOnlyWakeSeconds;
        if (!stalled && !idleOnly) return;

        int woken = 0;
        foreach (Zombie zombie in buffer)
            if (zombie.ForceChase(player)) woken++;
        Debug.Log($"IdleGroupDirector: 대기 적 {woken}마리 직접 각성 ({(idleOnly ? "대기 적만 남음" : "처치·생성 정체")}).", this);
        lastProgressTime = now;
        idleOnlySince = -1f;
        DormantCount = 0;
    }
}
