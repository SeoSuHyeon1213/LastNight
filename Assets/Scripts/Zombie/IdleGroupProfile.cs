using UnityEngine;

// 대기 좀비 군집 규칙 (기획서 6.5 "대기 좀비 배치"). 모두 테스트 시작값이다.
[CreateAssetMenu(menuName = "Scriptable/IdleGroupProfile", fileName = "Idle Group Profile")]
public sealed class IdleGroupProfile : ScriptableObject {
    [Header("수량: I(w) = min(최대, floor(비율 * B + 0.5)), 첫 웨이브 이전은 0")]
    [SerializeField, Min(1)] private int firstIdleWave = 2;
    [SerializeField, Min(0f)] private float ratioOfBaseCount = 0.2f;
    [SerializeField, Min(0)] private int maxIdlePerWave = 10;

    [Header("군집")]
    [SerializeField, Min(1)] private int minGroupSize = 1;
    [SerializeField, Min(1)] private int maxGroupSize = 3;
    [Tooltip("군집 구성원끼리의 최소 수평 거리")]
    [SerializeField, Min(0f)] private float memberSpacing = 1.2f;
    [Tooltip("구성원 위치를 NavMesh로 보정할 최대 거리")]
    [SerializeField, Min(0.05f)] private float navMeshSampleDistance = 1f;
    [SerializeField, Min(1)] private int attemptsPerMember = 6;
    [Header("최종 위치 검증 (일반 좀비 프리팹 크기·Agent와 일치시킨다)")]
    [SerializeField, Min(0.05f)] private float capsuleRadius = 0.4f;
    [SerializeField, Min(0.1f)] private float capsuleHeight = 2f;
    [SerializeField] private LayerMask blockingLayers = Physics.DefaultRaycastLayers;
    [SerializeField] private int agentTypeId = 0;
    [SerializeField] private int areaMask = -1;

    [Header("후보 지점 거리 (전투 시작 시 플레이어 기준, 후보값). 최대 0이면 제한 없음")]
    [SerializeField, Min(0f)] private float minPlayerDistance = 25f;
    [SerializeField, Min(0f)] private float maxPlayerDistance = 29f;

    [Header("정체 방지 (초, 일시정지 시 정지)")]
    [Tooltip("유효 처치·생성이 이 시간 동안 없고 대기 적이 있으면 직접 각성")]
    [SerializeField, Min(1f)] private float stallWakeSeconds = 30f;
    [Tooltip("살아 있는 적이 대기 적뿐인 상태가 이 시간 지속되면 직접 각성")]
    [SerializeField, Min(1f)] private float idleOnlyWakeSeconds = 30f;

    public int MinGroupSize => minGroupSize;
    public int MaxGroupSize => maxGroupSize;
    public float MemberSpacing => memberSpacing;
    public float NavMeshSampleDistance => navMeshSampleDistance;
    public int AttemptsPerMember => attemptsPerMember;
    public float CapsuleRadius => capsuleRadius;
    public float CapsuleHeight => capsuleHeight;
    public LayerMask BlockingLayers => blockingLayers;
    public int AgentTypeId => agentTypeId;
    public int AreaMask => areaMask;
    public float MinPlayerDistance => minPlayerDistance;
    public float MaxPlayerDistance => maxPlayerDistance;
    public float StallWakeSeconds => stallWakeSeconds;
    public float IdleOnlyWakeSeconds => idleOnlyWakeSeconds;

    // 예정 일반 수 B 중 대기 군집으로 둘 수. 추가 적이 아니라 B의 일부다.
    public int GetIdleCount(int wave, int baseCount) {
        if (wave < firstIdleWave || baseCount <= 0) return 0;
        int count = Mathf.FloorToInt(ratioOfBaseCount * baseCount + 0.5f);
        return Mathf.Clamp(count, 0, Mathf.Min(maxIdlePerWave, baseCount));
    }

    private void OnValidate() {
        capsuleHeight = Mathf.Max(capsuleRadius * 2f, capsuleHeight);
        maxGroupSize = Mathf.Max(minGroupSize, maxGroupSize);
        if (maxPlayerDistance > 0f) maxPlayerDistance = Mathf.Max(minPlayerDistance, maxPlayerDistance);
    }
}
