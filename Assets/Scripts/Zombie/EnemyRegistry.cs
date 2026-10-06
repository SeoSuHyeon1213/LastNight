using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

// 살아 있는 적 수, 소환자별 귀속 수, 생성 슬롯 예약을 한곳에서 관리한다.
// 웨이브 종료 판정과 소환 상한이 같은 수를 보도록 모든 적 생성은 예약 → 확정(또는 반환) 경로를 거친다.
public sealed class EnemyRegistry : MonoBehaviour {
    // 예약 번호. 확정 또는 반환은 예약당 한 번만 성공한다.
    public readonly struct SpawnReservation {
        public readonly int Id;
        public SpawnReservation(int id) { Id = id; }
    }

    // onDeath는 인자가 없으므로 어떤 적이 죽었는지 알기 위해 적마다 핸들을 두고, 같은 델리게이트로 해제한다.
    private sealed class Registration {
        public readonly Zombie Enemy;
        public readonly UnityEngine.Object Owner;
        public readonly Action DeathHandler;
        private readonly EnemyRegistry registry;

        public Registration(EnemyRegistry registry, Zombie enemy, UnityEngine.Object owner) {
            this.registry = registry;
            Enemy = enemy;
            Owner = owner;
            DeathHandler = HandleDeath;
        }

        private void HandleDeath() => registry.HandleDeath(this);
    }

    // UnityEngine.Object의 == 는 파괴된 오브젝트끼리 같다고 판단하므로 키 비교는 참조로 한다.
    private sealed class ReferenceComparer<T> : IEqualityComparer<T> where T : class {
        public bool Equals(T a, T b) => ReferenceEquals(a, b);
        public int GetHashCode(T obj) => RuntimeHelpers.GetHashCode(obj);
    }

    [Tooltip("동시에 살아 있을 수 있는 적 수(생성 예약 포함). 0이면 무제한.")]
    [SerializeField, Min(0)] private int maxAliveEnemies = 0;
    [SerializeField] private SpawnLocationValidator spawnValidator;
    public SpawnLocationValidator SpawnValidator => spawnValidator;

    private readonly Dictionary<Zombie, Registration> alive =
        new Dictionary<Zombie, Registration>(new ReferenceComparer<Zombie>());
    private readonly Dictionary<UnityEngine.Object, int> ownedCounts =
        new Dictionary<UnityEngine.Object, int>(new ReferenceComparer<UnityEngine.Object>());
    private readonly HashSet<int> pending = new HashSet<int>();
    private readonly List<Registration> pruneBuffer = new List<Registration>();
    private int nextReservationId = 1;
    private int waveQueueDemand; // 웨이브 예정 생성 대기 수. 소환 예약보다 우선한다.

    // 등록된 적이 사망해 집계에서 빠진 직후 한 번 호출된다.
    public event Action<Zombie> EnemyDied;
    // 적이 생성되어 등록(확정)된 직후 한 번 호출된다. 소환 적 포함.
    public event Action<Zombie> EnemyRegistered;

    public int AliveCount => alive.Count;
    public int PendingCount => pending.Count;
    public int MaxAliveEnemies => maxAliveEnemies;
    public bool HasCapacity => maxAliveEnemies <= 0 || alive.Count + pending.Count < maxAliveEnemies;
    // 웨이브 대기열 몫을 비워 둔 뒤에도 남는 슬롯이 있는지 (소환 등 후순위 예약용)
    public bool HasCapacityAfterWaveQueue =>
        maxAliveEnemies <= 0 || alive.Count + pending.Count + waveQueueDemand < maxAliveEnemies;

    // ZombieSpawner가 대기열이 바뀔 때마다 알린다.
    public void SetWaveQueueDemand(int count) {
        waveQueueDemand = Mathf.Max(0, count);
    }

    // 상한이 꺼져 있어도 예약을 발급해 확정/반환 경로가 항상 같게 동작하도록 한다.
    // yieldToWaveQueue가 true면 웨이브 예정 생성에 슬롯 우선권을 준다(기획서 6.2).
    public bool TryReserve(out SpawnReservation reservation, bool yieldToWaveQueue = false) {
        if (yieldToWaveQueue ? !HasCapacityAfterWaveQueue : !HasCapacity) {
            reservation = default;
            return false;
        }
        reservation = new SpawnReservation(nextReservationId++);
        pending.Add(reservation.Id);
        return true;
    }

    // 생성 실패·취소 시 슬롯을 돌려준다. 이미 확정·반환된 예약이면 false.
    public bool Release(SpawnReservation reservation) {
        if (pending.Remove(reservation.Id)) return true;
        Debug.LogWarning($"EnemyRegistry: 존재하지 않거나 이미 처리된 예약({reservation.Id})을 반환하려 했습니다.", this);
        return false;
    }

    // 예약 슬롯을 실제 적으로 확정한다. 실패하면 예약은 그대로 남으므로 호출자가 Release해야 한다.
    public bool Commit(SpawnReservation reservation, Zombie enemy, UnityEngine.Object owner = null) {
        if (!pending.Contains(reservation.Id)) {
            Debug.LogError($"EnemyRegistry: 유효하지 않은 예약({reservation.Id})으로 적을 등록하려 했습니다.", this);
            return false;
        }
        if (enemy == null || enemy.dead) {
            Debug.LogWarning("EnemyRegistry: 없거나 이미 사망한 적은 등록하지 않습니다.", this);
            return false;
        }
        if (alive.ContainsKey(enemy)) {
            Debug.LogWarning($"EnemyRegistry: {enemy.name}은(는) 이미 등록되어 있습니다.", enemy);
            return false;
        }

        pending.Remove(reservation.Id);
        Registration registration = new Registration(this, enemy, owner);
        alive.Add(enemy, registration);
        enemy.onDeath += registration.DeathHandler;
        if (!ReferenceEquals(owner, null)) ownedCounts[owner] = OwnedAliveCount(owner) + 1;
        if (enemy is IEnemyRegistryClient client) client.BindRegistry(this);
        EnemyRegistered?.Invoke(enemy);
        return true;
    }

    public bool IsRegistered(Zombie enemy) => enemy != null && alive.ContainsKey(enemy);

    // 살아 있는 적을 버퍼에 복사한다. 호출 중 사망으로 목록이 바뀌어도 안전하도록 복사본을 준다.
    public void CopyAliveEnemies(List<Zombie> buffer) {
        buffer.Clear();
        foreach (Zombie enemy in alive.Keys)
            if (enemy != null && !enemy.dead) buffer.Add(enemy);
    }

    public int OwnedAliveCount(UnityEngine.Object owner) {
        if (ReferenceEquals(owner, null)) return 0;
        return ownedCounts.TryGetValue(owner, out int count) ? count : 0;
    }

    // 사망 없이 파괴된 적이 남아 웨이브가 멈추지 않도록 집계에서 뺀다. 이 경우 EnemyDied는 호출하지 않는다.
    public void PruneDestroyed() {
        pruneBuffer.Clear();
        foreach (Registration registration in alive.Values)
            if (registration.Enemy == null) pruneBuffer.Add(registration);
        foreach (Registration registration in pruneBuffer) {
            alive.Remove(registration.Enemy);
            DecrementOwner(registration.Owner);
            Debug.LogWarning("EnemyRegistry: 사망 처리 없이 파괴된 적을 집계에서 제거했습니다.", this);
        }
        pruneBuffer.Clear();
    }

    private void HandleDeath(Registration registration) {
        // 같은 적의 사망이 두 번 전달되어도 한 번만 차감한다.
        if (!alive.TryGetValue(registration.Enemy, out Registration current) || current != registration) return;
        alive.Remove(registration.Enemy);
        registration.Enemy.onDeath -= registration.DeathHandler;
        DecrementOwner(registration.Owner);
        EnemyDied?.Invoke(registration.Enemy);
    }

    private void DecrementOwner(UnityEngine.Object owner) {
        if (ReferenceEquals(owner, null) || !ownedCounts.TryGetValue(owner, out int count)) return;
        if (count <= 1) ownedCounts.Remove(owner);
        else ownedCounts[owner] = count - 1;
    }

    private void OnDestroy() {
        foreach (Registration registration in alive.Values)
            if (registration.Enemy != null) registration.Enemy.onDeath -= registration.DeathHandler;
        alive.Clear();
        ownedCounts.Clear();
        pending.Clear();
        waveQueueDemand = 0;
        EnemyDied = null;
        EnemyRegistered = null;
    }
}
