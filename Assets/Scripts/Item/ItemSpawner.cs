using UnityEngine;
using UnityEngine.AI; // 내비메쉬 관련 코드

// 주기적으로 아이템을 플레이어 근처에 생성하는 스크립트
public class ItemSpawner : MonoBehaviour {
    public GameObject[] items; // 생성할 아이템들
    public Transform playerTransform; // 플레이어의 트랜스폼

    public float maxDistance = 5f; // 플레이어 위치로부터 아이템이 배치될 최대 반경

    [Header("Spawn Location")]
    [SerializeField, Min(0f)] private float maxHeightDifference = 0.75f;
    [SerializeField, Min(0.01f)] private float navMeshSampleDistance = 1f;
    [SerializeField, Min(1)] private int locationAttempts = 20;
    [SerializeField, Min(0f)] private float groundOffset = 0.5f;
    [SerializeField, Min(0.01f)] private float pickupClearance = 0.25f;
    [SerializeField] private LayerMask blockingLayers = Physics.DefaultRaycastLayers;
    [SerializeField] private int agentTypeId;
    private NavMeshPath spawnPath;
    private bool warnedNoSpawnPosition;
    public float timeBetSpawnMax = 7f; // 최대 시간 간격
    public float timeBetSpawnMin = 2f; // 최소 시간 간격
    private float timeBetSpawn; // 생성 간격

    private float lastSpawnTime; // 마지막 생성 시점

    private void Start() {
        var valid = new System.Collections.Generic.List<GameObject>();
        if (items != null) foreach (GameObject item in items)
            if (item != null && !valid.Contains(item)) valid.Add(item);
        GameObject planks = Resources.Load<GameObject>("Barricade/Plank Pickup");
        if (planks != null && !valid.Contains(planks)) valid.Add(planks);
        else if (planks == null) Debug.LogWarning("ItemSpawner: Tools/Zombie/Create Barricade Assets로 판자 픽업을 생성하세요.", this);
        GameObject grenadePickup = Resources.Load<GameObject>("Grenade Pickup");
        if (grenadePickup != null && !valid.Contains(grenadePickup)) valid.Add(grenadePickup);
        else if (grenadePickup == null) Debug.LogWarning("ItemSpawner: Resources/Grenade Pickup 프리팹을 찾을 수 없습니다.", this);
        items = valid.ToArray();
        if (items.Length == 0) {
            Debug.LogError("ItemSpawner: 생성 가능한 아이템이 없습니다.", this);
            enabled = false;
            return;
        }
        // 생성 간격과 마지막 생성 시점 초기화
        timeBetSpawn = Random.Range(timeBetSpawnMin, timeBetSpawnMax);
        lastSpawnTime = 0;
    }

    // 주기적으로 아이템 생성 처리 실행
    private void Update() {
        if (Time.timeScale <= 0f || (GameManager.instance != null && GameManager.instance.isGameover)) return;
        // 현재 시점이 마지막 생성 시점에서 생성 주기 이상 지남
        // && 플레이어 캐릭터가 존재함
        if (Time.time >= lastSpawnTime + timeBetSpawn && playerTransform != null)
        {
            // 마지막 생성 시간 갱신
            lastSpawnTime = Time.time;
            // 생성 주기를 랜덤으로 변경
            timeBetSpawn = Random.Range(timeBetSpawnMin, timeBetSpawnMax);
            // 아이템 생성 실행
            Spawn();
        }
    }

    // 실제 아이템 생성 처리
    private void Spawn() {
        // 플레이어 근처에서 내비메시 위의 랜덤 위치 가져오기
        if (!TryGetRandomPointOnNavMesh(playerTransform.position, maxDistance, out Vector3 spawnPosition)) {
            if (!warnedNoSpawnPosition)
                Debug.LogWarning("ItemSpawner: 플레이어와 비슷한 높이의 접근 가능한 위치를 찾지 못해 이번 생성을 건너뜁니다.", this);
            warnedNoSpawnPosition = true;
            return;
        }
        warnedNoSpawnPosition = false;
        // 검증된 바닥 위에 아이템 표시 높이를 더한다.
        spawnPosition += Vector3.up * groundOffset;

        // 아이템 중 하나를 무작위로 골라 랜덤 위치에 생성
        GameObject selectedItem = items[Random.Range(0, items.Length)];
        GameObject item = Instantiate(selectedItem, spawnPosition, Quaternion.identity);

        // 생성된 아이템을 5초 뒤에 파괴
        Destroy(item, 5f);
    }

    private bool TryGetRandomPointOnNavMesh(Vector3 center, float distance, out Vector3 position) {
        position = default;
        if (distance <= 0f) return false;
        var filter = new NavMeshQueryFilter { agentTypeID = agentTypeId, areaMask = NavMesh.AllAreas };
        if (!NavMesh.SamplePosition(center, out NavMeshHit playerHit, navMeshSampleDistance, filter) ||
            Mathf.Abs(playerHit.position.y - center.y) > maxHeightDifference) return false;
        if (spawnPath == null) spawnPath = new NavMeshPath();

        for (int attempt = 0; attempt < locationAttempts; attempt++) {
            Vector2 offset = Random.insideUnitCircle * distance;
            Vector3 candidate = center + new Vector3(offset.x, 0f, offset.y);
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, navMeshSampleDistance, filter)) continue;
            // SamplePosition은 다른 층을 고를 수 있으므로 보정된 최종 위치를 다시 검사한다.
            if (Mathf.Abs(hit.position.y - center.y) > maxHeightDifference) continue;
            Vector3 horizontal = hit.position - center;
            horizontal.y = 0f;
            if (horizontal.sqrMagnitude > distance * distance) continue;
            if (!NavMesh.CalculatePath(playerHit.position, hit.position, filter, spawnPath) ||
                spawnPath.status != NavMeshPathStatus.PathComplete) continue;
            if (Physics.CheckSphere(hit.position + Vector3.up * groundOffset, pickupClearance,
                blockingLayers, QueryTriggerInteraction.Ignore)) continue;
            position = hit.position;
            return true;
        }
        return false;
    }

    private void OnValidate() {
        maxDistance = Mathf.Max(0.01f, maxDistance);
        maxHeightDifference = Mathf.Max(0f, maxHeightDifference);
        navMeshSampleDistance = Mathf.Max(0.01f, navMeshSampleDistance);
        locationAttempts = Mathf.Max(1, locationAttempts);
        groundOffset = Mathf.Max(0f, groundOffset);
        pickupClearance = Mathf.Max(0.01f, pickupClearance);
    }
}
