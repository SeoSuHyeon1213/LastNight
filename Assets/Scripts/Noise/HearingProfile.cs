using UnityEngine;

// 좀비의 소음 청취·반응 규칙. 모두 기획서 6.5의 테스트 시작값이다.
[CreateAssetMenu(menuName = "Scriptable/HearingProfile", fileName = "Hearing Profile")]
public sealed class HearingProfile : ScriptableObject {
    [Header("상태별 감도 (청취 거리 = 소음 반경 × 감도, 0이면 무시)")]
    [SerializeField, Min(0f)] private float idleSensitivity = 1f;
    [SerializeField, Min(0f)] private float alertSensitivity = 1f;
    [SerializeField, Min(0f)] private float investigateSensitivity = 1f;
    [SerializeField, Min(0f)] private float searchSensitivity = 1f;
    [SerializeField, Min(0f)] private float chaseSensitivity = 0f;
    [Tooltip("거점 공격 중인 Wave 좀비의 감도")]
    [SerializeField, Min(0f)] private float attackBaseSensitivity = 0.5f;

    [Header("반응")]
    [SerializeField, Min(0f)] private float minReactionDelay = 0.5f;
    [SerializeField, Min(0f)] private float maxReactionDelay = 1f;
    [Tooltip("목표 위치 오차 = 거리 × 이 값 (최대값으로 제한)")]
    [SerializeField, Range(0f, 1f)] private float targetErrorRatio = 0.1f;
    [SerializeField, Min(0f)] private float maxTargetError = 3f;
    [Tooltip("한 번 반응한 뒤 다음 소음에 반응하기까지의 대기(초)")]
    [SerializeField, Min(0f)] private float reactCooldown = 3f;
    [Tooltip("새 소음의 체감 크기가 현재 목표보다 이 비율 이상 클 때만 목표를 바꾼다")]
    [SerializeField, Min(0f)] private float retargetMargin = 0.2f;

    [Header("조사·탐색")]
    [SerializeField, Min(0.1f)] private float investigateMaxDuration = 15f;
    [SerializeField, Min(0f)] private float searchDuration = 4f;
    [Tooltip("목표 지점을 NavMesh로 보정할 최대 거리. 실패하면 소음을 무시한다.")]
    [SerializeField, Min(0.1f)] private float navMeshSnapDistance = 3f;
    [Tooltip("이 거리 안에 들어오면 도착으로 보고 탐색으로 전환한다")]
    [SerializeField, Min(0.1f)] private float arriveDistance = 1f;

    public float MinReactionDelay => minReactionDelay;
    public float MaxReactionDelay => maxReactionDelay;
    public float TargetErrorRatio => targetErrorRatio;
    public float MaxTargetError => maxTargetError;
    public float ReactCooldown => reactCooldown;
    public float RetargetMargin => retargetMargin;
    public float InvestigateMaxDuration => investigateMaxDuration;
    public float SearchDuration => searchDuration;
    public float NavMeshSnapDistance => navMeshSnapDistance;
    public float ArriveDistance => arriveDistance;

    public float GetSensitivity(ZombieState state) {
        switch (state) {
            case ZombieState.Idle: return idleSensitivity;
            case ZombieState.Alert: return alertSensitivity;
            case ZombieState.Investigate: return investigateSensitivity;
            case ZombieState.Search: return searchSensitivity;
            case ZombieState.AttackBase: return attackBaseSensitivity;
            default: return chaseSensitivity;
        }
    }

    private void OnValidate() {
        maxReactionDelay = Mathf.Max(minReactionDelay, maxReactionDelay);
    }
}
