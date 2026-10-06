using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI; // AI, 내비게이션 시스템 관련 코드를 가져오기

// 소환자 상태 (기획서 6.2)
public enum SummonerState {
    Moving,       // 대기 지점으로 이동. 지정 대기 지점 시스템이 없어 NavMesh에 올라서면 바로 대기로 넘어간다
    Waiting,      // 다음 예고까지 대기. 기존 좀비 이동·공격은 계속한다
    Telegraphing, // 예고. 슬롯을 예약하고 표시를 띄운 채 이동·공격하지 않는다
    Summoning,    // 예고 완료 순간의 생성 처리
    Exhausted,    // 성공 횟수를 모두 사용. 더 이상 소환하지 않는다
    Dead
}

// 소환형 좀비. 횟수·간격·예고·귀속/전체 상한을 SummonProfile로 관리한다.
// 슬롯은 예고 시작 시 EnemyRegistry에 예약하고, 소환 완료 시 확정하며, 실패·취소·사망 시 반환한다.
public class CrowZombie : Zombie, ISpecialZombie, IEnemyRegistryClient {

    public ZombieData[] zombieDatas; // 소환할 좀비 데이터
    public Zombie[] zombiePrefab; // 소환할 좀비 원본 프리팹

    [Header("Summon")]
    [SerializeField] private SummonProfile summonProfile;
    [Tooltip("예고 위치마다 생성하는 표시 프리팹. 겹침 검사를 막지 않도록 일반 Collider 없이 구성한다.")]
    [SerializeField] private GameObject telegraphMarkerPrefab;
    [Tooltip("선택: 소환 가능 동안 켜 두고 소진·사망 시 끄는 자식 오브젝트")]
    [SerializeField] private GameObject summonerAura;

    public AudioSource spawnAudio;
    public AudioClip spawnSound;
    [SerializeField] private AudioClip telegraphSound;

    public SummonerState SummonState { get; private set; } = SummonerState.Moving;
    public int SuccessfulSummons { get; private set; }

    private EnemyRegistry enemyRegistry; // 이 소환자를 등록한 레지스트리 (등록 시 전달받음)
    private bool configChecked;
    private bool configValid;
    private float nextActionTime;
    private float telegraphEndTime;

    private readonly List<Vector3> plannedPositions = new List<Vector3>();
    private readonly List<EnemyRegistry.SpawnReservation> reservations = new List<EnemyRegistry.SpawnReservation>();
    private readonly List<GameObject> markers = new List<GameObject>();
    private readonly Collider[] overlapBuffer = new Collider[16];
    private NavMeshPath pathBuffer;
    private NavMeshAgent summonAgent;

    private const float CapsuleGroundOffset = 0.15f;

    // 소환자는 소음을 무시한다 (기획서 6.5: 변수 증가 방지)
    protected override bool ListensToNoise => false;

    public void BindRegistry(EnemyRegistry registry)
    {
        enemyRegistry = registry;
        summonAgent = GetComponent<NavMeshAgent>();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        ResetSummonState();
    }

    protected override void OnDisable()
    {
        CancelTelegraph();
        base.OnDisable();
    }

    // 예고 중 사망하면 소환·예약·표시를 모두 취소한다. 이미 소환된 적은 그대로 둔다.
    public override void Die()
    {
        if (dead) return;
        CancelTelegraph();
        SummonState = SummonerState.Dead;
        SetAura(false);
        base.Die();
    }

    // ISpecialZombie: 대기 중이면 다음 예고를 바로 시도하게 한다.
    public void SpecialAttack()
    {
        if (SummonState == SummonerState.Waiting) nextActionTime = Time.time;
    }

    // 물리 시간 기준이라 일시정지(timeScale 0) 중에는 호출되지 않는다.
    public void FixedUpdate()
    {
        if (dead || !IsConfigured()) return;

        if (GameManager.instance != null && GameManager.instance.isGameover)
        {
            if (SummonState == SummonerState.Telegraphing)
            {
                CancelTelegraph();
                SummonState = SummonerState.Waiting;
            }
            return;
        }

        switch (SummonState)
        {
            case SummonerState.Moving:
                if (IsAgentReady)
                {
                    SummonState = SummonerState.Waiting;
                    nextActionTime = Time.time + summonProfile.FirstDelaySeconds;
                }
                break;
            case SummonerState.Waiting:
                if (Time.time >= nextActionTime && !IsAttacking) TryBeginTelegraph();
                break;
            case SummonerState.Telegraphing:
                if (Time.time >= telegraphEndTime) CompleteSummon();
                break;
        }
    }

    // 귀속 상한·전체 상한 안에서 위치와 슬롯을 먼저 확보한 뒤에만 예고를 시작한다.
    private void TryBeginTelegraph()
    {
        int ownedRoom = summonProfile.MaxOwnedAlive - enemyRegistry.OwnedAliveCount(this);
        int wanted = Mathf.Min(summonProfile.SummonCount, ownedRoom);
        if (wanted <= 0)
        {
            ScheduleRetry();
            return;
        }

        FindSpawnPositions(wanted);
        reservations.Clear();
        for (int i = 0; i < plannedPositions.Count; i++)
        {
            // 웨이브 예정 생성이 대기 중이면 그쪽에 슬롯 우선권을 준다
            if (!enemyRegistry.TryReserve(out EnemyRegistry.SpawnReservation reservation, true)) break;
            reservations.Add(reservation);
        }
        if (plannedPositions.Count > reservations.Count)
            plannedPositions.RemoveRange(reservations.Count, plannedPositions.Count - reservations.Count);

        if (reservations.Count == 0)
        {
            plannedPositions.Clear();
            ScheduleRetry();
            return;
        }

        SummonState = SummonerState.Telegraphing;
        telegraphEndTime = Time.time + summonProfile.TelegraphSeconds;
        SetMovementLocked(true);
        if (telegraphMarkerPrefab != null)
            foreach (Vector3 position in plannedPositions)
                markers.Add(Instantiate(telegraphMarkerPrefab, position, Quaternion.identity));
        if (spawnAudio != null && telegraphSound != null) spawnAudio.PlayOneShot(telegraphSound);
    }

    // 예고 완료 시 위치를 다시 검사해 유효한 곳만 생성한다. 하나라도 생성하면 횟수 1을 쓰고, 전부 실패하면 횟수를 유지하고 재시도한다.
    private void CompleteSummon()
    {
        SummonState = SummonerState.Summoning;
        int spawned = 0;
        for (int i = 0; i < reservations.Count; i++)
        {
            Vector3 position = plannedPositions[i];
            if (IsValidSpawnPosition(position) && SpawnAt(position, reservations[i])) spawned++;
            else enemyRegistry.Release(reservations[i]);
        }
        reservations.Clear();
        plannedPositions.Clear();
        ClearMarkers();
        SetMovementLocked(false);

        if (spawned == 0)
        {
            ScheduleRetry();
            return;
        }

        SpawnEffect(transform.position);
        SuccessfulSummons++;
        if (SuccessfulSummons >= summonProfile.MaxSuccessfulSummons)
        {
            SummonState = SummonerState.Exhausted;
            SetAura(false);
            return;
        }
        SummonState = SummonerState.Waiting;
        nextActionTime = Time.time + summonProfile.CooldownSeconds;
    }

    private bool SpawnAt(Vector3 position, EnemyRegistry.SpawnReservation reservation)
    {
        Zombie zombie = null;
        bool committed = false;
        try
        {
            ZombieData zombieData = zombieDatas[Random.Range(0, zombieDatas.Length)];
            Vector3 facing = position - transform.position;
            facing.y = 0f;
            Quaternion rotation = facing.sqrMagnitude > 0.001f ? Quaternion.LookRotation(facing) : transform.rotation;
            zombie = Instantiate(zombiePrefab[Random.Range(0, zombiePrefab.Length)], position, rotation);
            zombie.Setup(zombieData);
            // 소환된 좀비도 Wave 역할로 이 소환자와 같은 보관소를 목표로 삼는다
            zombie.AssignRole(ZombieRole.Wave, BaseTarget);
            // 이 소환자에게 귀속해 등록. 사망 집계·점수·시체 제거는 레지스트리 경로에서 처리
            committed = enemyRegistry.Commit(reservation, zombie, this);
        }
        finally
        {
            // 확정하지 못한 예약은 호출자가 반환하므로 여기서는 생성물만 정리한다
            if (!committed && zombie != null) Destroy(zombie.gameObject);
        }
        return committed;
    }

    // 2~4m 고리 안에서 NavMesh·캡슐 충돌·플레이어 거리·서로 간격·소환자로부터의 경로를 만족하는 위치를 찾는다.
    private void FindSpawnPositions(int wanted)
    {
        plannedPositions.Clear();
        if (pathBuffer == null) pathBuffer = new NavMeshPath();
        Vector3 origin = transform.position;

        for (int slot = 0; slot < wanted; slot++)
        {
            for (int attempt = 0; attempt < summonProfile.CandidateAttempts; attempt++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float radius = Random.Range(summonProfile.MinRadius, summonProfile.MaxRadius);
                Vector3 candidate = origin + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                if (!enemyRegistry.SpawnValidator.TrySnap(candidate, summonProfile.NavMeshSampleDistance,
                    summonAgent.agentTypeID, NavAreaMask, out Vector3 position))
                    continue;
                Vector3 flat = position - origin;
                flat.y = 0f;
                float distance = flat.magnitude;
                if (distance < summonProfile.MinRadius || distance > summonProfile.MaxRadius) continue;
                if (!IsFarFromPlanned(position) || !IsValidSpawnPosition(position)) continue;
                if (!NavMesh.CalculatePath(origin, position, NavAreaMask, pathBuffer) ||
                    pathBuffer.status != NavMeshPathStatus.PathComplete) continue;

                plannedPositions.Add(position);
                break;
            }
        }
    }

    private bool IsFarFromPlanned(Vector3 position)
    {
        float minSqr = summonProfile.MinSpacing * summonProfile.MinSpacing;
        foreach (Vector3 other in plannedPositions)
        {
            Vector3 offset = position - other;
            offset.y = 0f;
            if (offset.sqrMagnitude < minSqr) return false;
        }
        return true;
    }

    // 예고 시작과 완료 시 모두 쓰는 검사: 캡슐 겹침 없음 + 살아 있는 플레이어와 최소 거리 이상
    private bool IsSpotClear(Vector3 position)
    {
        float radius = summonProfile.CapsuleRadius;
        Vector3 bottom = position + Vector3.up * (CapsuleGroundOffset + radius);
        Vector3 top = position + Vector3.up * Mathf.Max(CapsuleGroundOffset + radius, summonProfile.CapsuleHeight - radius);
        if (Physics.CheckCapsule(bottom, top, radius, summonProfile.BlockingLayers, QueryTriggerInteraction.Ignore))
            return false;

        int count = Physics.OverlapSphereNonAlloc(position, summonProfile.MinPlayerDistance, overlapBuffer,
            whatIsTarget, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++)
        {
            PlayerHealth player = overlapBuffer[i].GetComponentInParent<PlayerHealth>();
            if (player != null && !player.dead) return false;
        }
        return true;
    }

    private void ScheduleRetry()
    {
        SummonState = SummonerState.Waiting;
        nextActionTime = Time.time + summonProfile.RetryDelaySeconds;
    }

    private bool IsValidSpawnPosition(Vector3 position) {
        if (!IsSpotClear(position) || enemyRegistry.SpawnValidator == null || summonAgent == null) return false;
        if (!enemyRegistry.SpawnValidator.Validate(position, summonProfile.CapsuleRadius, summonProfile.CapsuleHeight,
            summonProfile.BlockingLayers, position, 0f, 0f, summonAgent.agentTypeID, NavAreaMask)) return false;
        if (pathBuffer == null) pathBuffer = new NavMeshPath();
        return NavMesh.CalculatePath(transform.position, position,
            new NavMeshQueryFilter { agentTypeID = summonAgent.agentTypeID, areaMask = NavAreaMask }, pathBuffer) &&
            pathBuffer.status == NavMeshPathStatus.PathComplete;
    }

    // 예약 반환·표시 제거·이동 잠금 해제. 상태 전환은 호출자가 정한다.
    private void CancelTelegraph()
    {
        if (enemyRegistry != null)
            foreach (EnemyRegistry.SpawnReservation reservation in reservations) enemyRegistry.Release(reservation);
        reservations.Clear();
        plannedPositions.Clear();
        ClearMarkers();
        if (SummonState == SummonerState.Telegraphing || SummonState == SummonerState.Summoning)
            SetMovementLocked(false);
    }

    private void ClearMarkers()
    {
        foreach (GameObject marker in markers)
            if (marker != null) Destroy(marker);
        markers.Clear();
    }

    private void ResetSummonState()
    {
        SummonState = SummonerState.Moving;
        SuccessfulSummons = 0;
        nextActionTime = 0f;
        telegraphEndTime = 0f;
        reservations.Clear();
        plannedPositions.Clear();
        ClearMarkers();
        SetAura(true);
    }

    private void SetAura(bool active)
    {
        if (summonerAura != null) summonerAura.SetActive(active);
    }

    private bool IsConfigured()
    {
        if (configChecked) return configValid;
        if (enemyRegistry == null) return false; // 등록 직후 전달받을 때까지 기다린다
        configChecked = true;
        configValid = summonProfile != null && summonAgent != null &&
            enemyRegistry.SpawnValidator != null && enemyRegistry.SpawnValidator.IsConfigured &&
            zombiePrefab != null && zombiePrefab.Length > 0 && System.Array.TrueForAll(zombiePrefab, p => p != null) &&
            zombieDatas != null && zombieDatas.Length > 0 && System.Array.TrueForAll(zombieDatas, d => d != null);
        if (!configValid)
            Debug.LogError("CrowZombie: Summon Profile, Zombie Prefab, Zombie Datas 및 레지스트리의 Spawn Validator 연결을 확인하세요.", this);
        return configValid;
    }

    public void SpawnEffect(Vector3 position)
    {
        if (spawnAudio != null && spawnSound != null) spawnAudio.PlayOneShot(spawnSound);
    }

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();
        if (summonProfile == null) return;
#if UNITY_EDITOR
        UnityEditor.Handles.color = new Color(0.6f, 0.2f, 1f, 0.8f);
        UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.up, summonProfile.MinRadius);
        UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.up, summonProfile.MaxRadius);
        if (Application.isPlaying)
            UnityEditor.Handles.Label(transform.position + Vector3.up * 3f,
                $"{SummonState}  {SuccessfulSummons}/{summonProfile.MaxSuccessfulSummons}");
#endif
        Gizmos.color = new Color(0.6f, 0.2f, 1f, 0.9f);
        foreach (Vector3 position in plannedPositions)
            Gizmos.DrawWireSphere(position + Vector3.up * summonProfile.CapsuleHeight * 0.5f, summonProfile.CapsuleRadius);
    }
}
