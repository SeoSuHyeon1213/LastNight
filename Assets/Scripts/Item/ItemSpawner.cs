using System;
using UnityEngine;
using UnityEngine.AI; // 내비메쉬 관련 코드

// 주기적으로 아이템을 플레이어 근처에 생성하는 스크립트
public class ItemSpawner : MonoBehaviour {
    [Serializable]
    public sealed class SpawnEntry {
        public GameObject prefab;
        [Tooltip("0이면 추첨에서 제외한다. 나머지 항목과의 상대 비중이다.")]
        [Min(0)] public int weight = 1;
        [Tooltip("아이템 1개 획득 시 지급량. 탄약·판자·수류탄 수, 회복량 또는 코인 점수다.")]
        [Min(1)] public int amount = 1;
    }

    [Header("Indoor / Outdoor Loot")]
    [Tooltip("실내 구역을 정의한 기존 BaseHouse를 모두 연결한다. 거점 후보 경로의 유효성과는 별개다.")]
    [SerializeField] private BaseHouse[] houses;
    [SerializeField] private SpawnEntry[] indoorItems;
    [SerializeField] private SpawnEntry[] outdoorItems;
    [SerializeField] private Transform playerTransform;

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
    [SerializeField, Min(0.01f)] private float itemLifetime = 5f;
    [SerializeField, Min(0.01f)] private float timeBetSpawnMax = 7f;
    [SerializeField, Min(0.01f)] private float timeBetSpawnMin = 2f;
    private float timeBetSpawn; // 생성 간격

    private float lastSpawnTime; // 마지막 생성 시점
    private bool warnedEmptyIndoor;
    private bool warnedEmptyOutdoor;

    private void Start() {
        if (playerTransform == null || houses == null || houses.Length == 0 ||
            !Array.TrueForAll(houses, house => house != null && house.HasIndoorZones)) {
            Debug.LogError("ItemSpawner: Player Transform과 실내 구역이 설정된 Houses를 연결하세요.", this);
            enabled = false;
            return;
        }
        ValidateEntries(indoorItems, "Indoor Items");
        ValidateEntries(outdoorItems, "Outdoor Items");
        if (GetTotalWeight(indoorItems) == 0 && GetTotalWeight(outdoorItems) == 0) {
            Debug.LogError("ItemSpawner: 실내·실외 목록 중 하나 이상에 유효한 아이템을 연결하세요.", this);
            enabled = false;
            return;
        }
        // 생성 간격과 마지막 생성 시점 초기화
        timeBetSpawn = UnityEngine.Random.Range(timeBetSpawnMin, timeBetSpawnMax);
        lastSpawnTime = Time.time;
    }

    // 주기적으로 아이템 생성 처리 실행
    private void Update() {
        if (!CanSpawn) return;
        // 현재 시점이 마지막 생성 시점에서 생성 주기 이상 지남
        // && 플레이어 캐릭터가 존재함
        if (Time.time >= lastSpawnTime + timeBetSpawn && playerTransform != null)
        {
            // 마지막 생성 시간 갱신
            lastSpawnTime = Time.time;
            // 생성 주기를 랜덤으로 변경
            timeBetSpawn = UnityEngine.Random.Range(timeBetSpawnMin, timeBetSpawnMax);
            // 아이템 생성 실행
            Spawn();
        }
    }

    // 실제 아이템 생성 처리
    private void Spawn() {
        if (!CanSpawn || playerTransform == null) return;
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

        SpawnAt(spawnPosition);
    }

    private bool CanSpawn => isActiveAndEnabled && Time.timeScale > 0f &&
        (GameManager.instance == null || !GameManager.instance.isGameover) &&
        (GameSessionBase.instance == null || (!GameSessionBase.instance.IsPaused && !GameSessionBase.instance.IsRebinding));

    public bool IsIndoors(Vector3 position) {
        if (houses == null) return false;
        foreach (BaseHouse house in houses)
            if (house != null && house.Contains(position)) return true;
        return false;
    }

    // NavMesh 보정과 표시 높이를 적용한 실제 생성 위치로 판정한다. 빈 목록은 다른 구역으로 대체하지 않는다.
    private GameObject SpawnAt(Vector3 spawnPosition) {
        if (!CanSpawn) return null;
        bool indoors = IsIndoors(spawnPosition);
        SpawnEntry[] entries = indoors ? indoorItems : outdoorItems;
        int total = GetTotalWeight(entries);
        if (total == 0) {
            bool warned = indoors ? warnedEmptyIndoor : warnedEmptyOutdoor;
            if (!warned) Debug.LogWarning($"ItemSpawner: {(indoors ? "실내" : "실외")} 목록이 비어 이번 생성을 건너뜁니다.", this);
            if (indoors) warnedEmptyIndoor = true; else warnedEmptyOutdoor = true;
            return null;
        }
        if (indoors) warnedEmptyIndoor = false; else warnedEmptyOutdoor = false;
        int roll = UnityEngine.Random.Range(0, total);
        foreach (SpawnEntry entry in entries) {
            if (!IsValidEntry(entry)) continue;
            if (roll >= entry.weight) { roll -= entry.weight; continue; }
            GameObject item = Instantiate(entry.prefab, spawnPosition, Quaternion.identity);
            item.GetComponent<IItemSpawnAmount>().SetSpawnAmount(entry.amount);
            Destroy(item, itemLifetime);
            return item;
        }
        return null;
    }

    private static bool IsValidEntry(SpawnEntry entry) => entry != null && entry.prefab != null &&
        entry.prefab.activeSelf && entry.weight > 0 && entry.amount > 0 &&
        entry.prefab.GetComponent<IItem>() != null && entry.prefab.GetComponent<IItemSpawnAmount>() != null;

    private static int GetTotalWeight(SpawnEntry[] entries) {
        long total = 0;
        if (entries != null) foreach (SpawnEntry entry in entries)
            if (IsValidEntry(entry)) total += entry.weight;
        return total <= int.MaxValue ? (int)total : 0;
    }

    private void ValidateEntries(SpawnEntry[] entries, string label) {
        if (entries != null) for (int i = 0; i < entries.Length; i++) {
            SpawnEntry entry = entries[i];
            if (entry != null && entry.weight == 0) continue;
            if (!IsValidEntry(entry))
                Debug.LogWarning($"ItemSpawner: {label}[{i}]를 제외합니다. 활성 프리팹, IItem/IItemSpawnAmount, 양수 지급량·가중치를 확인하세요.", this);
        }
        long total = 0;
        if (entries != null) foreach (SpawnEntry entry in entries)
            if (IsValidEntry(entry)) total += entry.weight;
        if (total > int.MaxValue) Debug.LogError($"ItemSpawner: {label} 가중치 합계가 int 범위를 초과해 해당 구역 생성을 중단합니다.", this);
    }

    private bool TryGetRandomPointOnNavMesh(Vector3 center, float distance, out Vector3 position) {
        position = default;
        if (distance <= 0f) return false;
        var filter = new NavMeshQueryFilter { agentTypeID = agentTypeId, areaMask = NavMesh.AllAreas };
        if (!NavMesh.SamplePosition(center, out NavMeshHit playerHit, navMeshSampleDistance, filter) ||
            Mathf.Abs(playerHit.position.y - center.y) > maxHeightDifference) return false;
        if (spawnPath == null) spawnPath = new NavMeshPath();

        for (int attempt = 0; attempt < locationAttempts; attempt++) {
            Vector2 offset = UnityEngine.Random.insideUnitCircle * distance;
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
        timeBetSpawnMin = Mathf.Max(0.01f, timeBetSpawnMin);
        timeBetSpawnMax = Mathf.Max(timeBetSpawnMin, timeBetSpawnMax);
        itemLifetime = Mathf.Max(0.01f, itemLifetime);
    }
}
