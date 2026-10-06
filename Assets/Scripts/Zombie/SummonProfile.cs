using UnityEngine;

// 소환자 소환 규칙. 기획서 6.2의 테스트 시작값이며 확정 밸런스가 아니다.
[CreateAssetMenu(menuName = "Scriptable/SummonProfile", fileName = "Summon Profile")]
public sealed class SummonProfile : ScriptableObject {
    [Header("횟수·수량")]
    [Tooltip("1회 소환 시 생성하는 최대 수")]
    [SerializeField, Min(1)] private int summonCount = 2;
    [Tooltip("성공한 소환 횟수 상한. 도달하면 소진 상태가 된다.")]
    [SerializeField, Min(1)] private int maxSuccessfulSummons = 3;
    [Tooltip("이 소환자가 만든 살아 있는 적 + 예약 슬롯의 최대 수")]
    [SerializeField, Min(1)] private int maxOwnedAlive = 4;

    [Header("시간 (초, 일시정지 시 정지)")]
    [SerializeField, Min(0f)] private float firstDelaySeconds = 4f;
    [SerializeField, Min(0f)] private float telegraphSeconds = 2f;
    [Tooltip("성공한 소환 후 다음 예고까지")]
    [SerializeField, Min(0f)] private float cooldownSeconds = 12f;
    [Tooltip("위치·슬롯 확보 실패 또는 전부 실패 시 재시도까지")]
    [SerializeField, Min(0.1f)] private float retryDelaySeconds = 3f;

    [Header("위치 검사")]
    [SerializeField, Min(0f)] private float minRadius = 2f;
    [SerializeField, Min(0f)] private float maxRadius = 4f;
    [SerializeField, Min(0f)] private float minPlayerDistance = 3f;
    [Tooltip("생성 위치끼리의 최소 수평 거리")]
    [SerializeField, Min(0f)] private float minSpacing = 1f;
    [Tooltip("위치 1개당 무작위 후보 시도 횟수")]
    [SerializeField, Min(1)] private int candidateAttempts = 8;
    [Tooltip("후보 지점을 NavMesh로 보정할 최대 거리")]
    [SerializeField, Min(0.05f)] private float navMeshSampleDistance = 1f;
    [Tooltip("생성될 좀비 크기로 겹침을 검사할 캡슐")]
    [SerializeField, Min(0.05f)] private float capsuleRadius = 0.4f;
    [SerializeField, Min(0.1f)] private float capsuleHeight = 2f;
    [Tooltip("캡슐 겹침으로 막힌 것으로 보는 레이어 (바닥 제외 권장, 트리거는 무시)")]
    [SerializeField] private LayerMask blockingLayers = Physics.DefaultRaycastLayers;

    public int SummonCount => summonCount;
    public int MaxSuccessfulSummons => maxSuccessfulSummons;
    public int MaxOwnedAlive => maxOwnedAlive;
    public float FirstDelaySeconds => firstDelaySeconds;
    public float TelegraphSeconds => telegraphSeconds;
    public float CooldownSeconds => cooldownSeconds;
    public float RetryDelaySeconds => retryDelaySeconds;
    public float MinRadius => minRadius;
    public float MaxRadius => maxRadius;
    public float MinPlayerDistance => minPlayerDistance;
    public float MinSpacing => minSpacing;
    public int CandidateAttempts => candidateAttempts;
    public float NavMeshSampleDistance => navMeshSampleDistance;
    public float CapsuleRadius => capsuleRadius;
    public float CapsuleHeight => capsuleHeight;
    public LayerMask BlockingLayers => blockingLayers;

    private void OnValidate() {
        maxRadius = Mathf.Max(minRadius, maxRadius);
        capsuleHeight = Mathf.Max(capsuleRadius * 2f, capsuleHeight);
    }
}
