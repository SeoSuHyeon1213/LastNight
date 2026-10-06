using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

// 소음 처리 주체. 발생한 소음을 모았다가 프레임마다 한 번, EnemyRegistry의 적 목록에서 청취 가능한 좀비를 골라 반응을 요청한다.
// 소음이 없는 프레임에는 적 목록을 훑지 않는다. 반응 판정과 상태 전환은 각 좀비가 맡는다.
public sealed class NoiseDispatcher : MonoBehaviour {
    private struct Candidate {
        public Zombie Listener;
        public int EventIndex;
        public float Distance;
        public float Perceived;
    }

    private sealed class ReferenceComparer : IEqualityComparer<Zombie> {
        public bool Equals(Zombie a, Zombie b) => ReferenceEquals(a, b);
        public int GetHashCode(Zombie obj) => RuntimeHelpers.GetHashCode(obj);
    }

    [SerializeField] private EnemyRegistry enemyRegistry;
    [Tooltip("소음 1회당 반응하는 최대 좀비 수 (가까운 순)")]
    [SerializeField, Min(1)] private int maxListenersPerNoise = 8;

    [Header("Debug")]
    [SerializeField] private bool drawGizmos = true;
    [SerializeField, Min(0.1f)] private float gizmoDuration = 2f;

    private static readonly Comparison<Candidate> ByDistance = (a, b) => a.Distance.CompareTo(b.Distance);

    private readonly List<NoiseEvent> pending = new List<NoiseEvent>();
    private readonly List<Zombie> enemies = new List<Zombie>();
    private readonly List<Candidate> candidates = new List<Candidate>();
    private readonly Dictionary<Zombie, Candidate> chosen = new Dictionary<Zombie, Candidate>(new ReferenceComparer());

    private void Awake() {
        if (enemyRegistry == null) {
            Debug.LogError("NoiseDispatcher: Enemy Registry를 연결하세요.", this);
            enabled = false;
        }
    }

    // 소음원이 호출한다. 실제 판정은 같은 프레임의 Update에서 모아서 처리한다.
    public void Emit(in NoiseEvent noise) {
        if (!isActiveAndEnabled || noise.Radius <= 0f || IsGameOver) return;
        pending.Add(noise);
        RecordDebugNoise(noise);
    }

    private static bool IsGameOver => GameManager.instance != null && GameManager.instance.isGameover;

    private void Update() {
        if (IsGameOver) {
            pending.Clear();
            return;
        }
        if (pending.Count == 0 || Time.timeScale <= 0f) return;
        ProcessPending();
    }

    private void OnDisable() {
        pending.Clear();
        chosen.Clear();
        candidates.Clear();
        enemies.Clear();
    }

    // 같은 프레임의 소음을 함께 처리한다. 각 소음은 가까운 순으로 상한까지 후보를 고르고,
    // 여러 소음에 뽑힌 좀비는 체감 크기가 가장 큰 소음 하나에만 반응한다.
    private void ProcessPending() {
        enemyRegistry.CopyAliveEnemies(enemies);
        chosen.Clear();

        for (int eventIndex = 0; eventIndex < pending.Count; eventIndex++) {
            NoiseEvent noise = pending[eventIndex];
            candidates.Clear();
            float radiusSqr = noise.Radius * noise.Radius;
            foreach (Zombie zombie in enemies) {
                if (zombie == null) continue;
                Vector3 offset = zombie.transform.position - noise.Position;
                if (offset.sqrMagnitude > radiusSqr) continue;
                float distance = offset.magnitude;
                if (!zombie.CanHear(noise, distance, out float perceived)) continue;
                candidates.Add(new Candidate {
                    Listener = zombie, EventIndex = eventIndex, Distance = distance, Perceived = perceived
                });
            }

            candidates.Sort(ByDistance);
            int count = Mathf.Min(maxListenersPerNoise, candidates.Count);
            for (int i = 0; i < count; i++) {
                Candidate candidate = candidates[i];
                if (!chosen.TryGetValue(candidate.Listener, out Candidate previous) ||
                    candidate.Perceived > previous.Perceived)
                    chosen[candidate.Listener] = candidate;
            }
        }

        foreach (Candidate candidate in chosen.Values) {
            NoiseEvent noise = pending[candidate.EventIndex];
            if (candidate.Listener != null && candidate.Listener.HearNoise(noise, candidate.Distance, candidate.Perceived))
                RecordDebugReaction(noise, candidate.Listener);
        }

        pending.Clear();
        chosen.Clear();
        candidates.Clear();
        enemies.Clear();
    }

#if UNITY_EDITOR
    private struct DebugNoise { public NoiseEvent Noise; public float Time; }
    private struct DebugLine { public Vector3 From; public Vector3 To; public float Time; }
    private const int MaxDebugEntries = 64;
    private readonly List<DebugNoise> debugNoises = new List<DebugNoise>();
    private readonly List<DebugLine> debugLines = new List<DebugLine>();

    private void RecordDebugNoise(in NoiseEvent noise) {
        if (!drawGizmos) return;
        if (debugNoises.Count >= MaxDebugEntries) debugNoises.RemoveAt(0);
        debugNoises.Add(new DebugNoise { Noise = noise, Time = Time.time });
    }

    private void RecordDebugReaction(in NoiseEvent noise, Zombie listener) {
        if (!drawGizmos) return;
        if (debugLines.Count >= MaxDebugEntries) debugLines.RemoveAt(0);
        debugLines.Add(new DebugLine { From = noise.Position, To = listener.transform.position, Time = Time.time });
    }

    // 총성 빨강, 이동 노랑, 조준 이동 초록, 재장전 파랑, 획득 흰색, 바리케이드 작업 보라
    private static Color GetDebugColor(NoiseKind kind) {
        switch (kind) {
            case NoiseKind.Gunshot: return Color.red;
            case NoiseKind.Movement: return Color.yellow;
            case NoiseKind.AimMovement: return Color.green;
            case NoiseKind.Reload: return new Color(0.3f, 0.5f, 1f);
            case NoiseKind.ItemPickup: return Color.white;
            default: return new Color(0.7f, 0.3f, 1f);
        }
    }

    // 최근 소음 반경과 반응한 좀비로 향하는 선을 Scene 뷰에 표시한다.
    private void OnDrawGizmos() {
        if (!drawGizmos || !Application.isPlaying) return;
        float now = Time.time;
        foreach (DebugNoise entry in debugNoises) {
            float age = now - entry.Time;
            if (age > gizmoDuration) continue;
            Color color = GetDebugColor(entry.Noise.Kind);
            color.a = 1f - age / gizmoDuration;
            Gizmos.color = color;
            Gizmos.DrawWireSphere(entry.Noise.Position, entry.Noise.Radius);
        }
        foreach (DebugLine line in debugLines) {
            float age = now - line.Time;
            if (age > gizmoDuration) continue;
            Gizmos.color = new Color(1f, 0.5f, 0f, 1f - age / gizmoDuration);
            Gizmos.DrawLine(line.From, line.To);
        }
    }
#else
    private void RecordDebugNoise(in NoiseEvent noise) { }
    private void RecordDebugReaction(in NoiseEvent noise, Zombie listener) { }
#endif
}
