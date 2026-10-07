using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

// 좀비 게임 오브젝트를 웨이브 단위로 생성
// 웨이브 단계(준비·생성·정리·종료)와 예정 생성 대기열을 소유한다. 살아 있는 적 수와 생성 슬롯은 EnemyRegistry가 소유한다.
// 일반 웨이브는 기존처럼 한 프레임에 일괄 생성하고 전멸 즉시 다음 웨이브로 넘어간다. 빅웨이브만 준비 시간과 분할 생성을 쓴다.
public class ZombieSpawner : MonoBehaviour {
    [SerializeField] private Zombie[] zombiePrefab; // 생성할 좀비 원본 프리팹
    [SerializeField] private ZombieData[] zombieDatas; // 사용할 좀비 셋업 데이터들
    [SerializeField] private Transform[] spawnPoints; // 좀비 AI를 소환할 위치들
    [SerializeField] private EnemyRegistry enemyRegistry; // 살아 있는 적 집계와 생성 슬롯
    [Tooltip("선택: 대기 군집 배치와 정체 방지 각성. 비워 두면 모든 물량을 스폰 지점에서 생성한다.")]
    [SerializeField] private IdleGroupDirector idleGroupDirector;
    [Tooltip("Wave 역할 좀비가 대상이 없을 때 공격할 보관소. 비우면 Wave 좀비는 정지 상태로 대기한다.")]
    [SerializeField] private BaseStorage baseTarget;

    [Header("Wave Tuning")]
    [Tooltip("웨이브당 생성 수 = RoundToInt(wave * 이 값)")]
    [SerializeField, Min(0f)] private float spawnCountPerWave = 1.5f;
    [Tooltip("사망한 적을 파괴하기까지의 시간(초)")]
    [SerializeField, Min(0f)] private float corpseDestroyDelay = 10f;
    [SerializeField, Min(0)] private int scorePerKill = 100;

    [Header("Big Wave")]
    [Tooltip("이 값의 배수 웨이브가 빅웨이브. 0이면 빅웨이브를 쓰지 않는다.")]
    [SerializeField, Min(0)] private int bigWaveInterval = 10;
    [SerializeField, Min(0f)] private float bigWavePreparationSeconds = 20f;
    [Tooltip("빅웨이브 생성 수 = CeilToInt(기본 생성 수 * 이 값)")]
    [SerializeField, Min(0f)] private float bigWaveCountMultiplier = 1.5f;
    [Tooltip("한 묶음에 생성하는 최대 수. 묶음 안에서도 한 프레임에 한 마리씩 생성한다.")]
    [SerializeField, Min(1)] private int bigWaveBatchSize = 5;
    [SerializeField, Min(0.01f)] private float bigWaveBatchInterval = 0.75f;

    [Header("Test")]
    [Tooltip("첫 웨이브 번호. 테스트용이며 기본값 1이 실제 게임 진행이다.")]
    [SerializeField, Min(1)] private int startWave = 1;

    public int wave { get; private set; } // 현재 웨이브
    public WavePhase Phase { get; private set; } = WavePhase.None;
    public bool IsBigWave => IsBigWaveNumber(wave);
    public bool NextWaveIsBigWave => IsBigWaveNumber(wave + 1);
    // 아직 생성되지 않은 예정 수 (일반 + 대기 군집)
    public int QueuedSpawnCount => spawnQueue + idleQueue.Count;
    // 아직 생성되지 않은 예정 수 + 살아 있는 적 수 (소환 적 포함)
    public int RemainingEnemyCount => QueuedSpawnCount + (enemyRegistry != null ? enemyRegistry.AliveCount : 0);
    public float PreparationTimeRemaining => Phase == WavePhase.Preparing ? Mathf.Max(0f, preparationEndTime - Time.time) : 0f;

    // 단계가 바뀔 때마다 한 번 호출된다. 같은 프레임에 여러 번 바뀌면 순서대로 모두 호출된다.
    public event Action<WaveStatus> PhaseChanged;

    private readonly List<Zombie> validPrefabs = new List<Zombie>();
    private readonly List<ZombieData> validDatas = new List<ZombieData>();
    private readonly List<Transform> validSpawnPoints = new List<Transform>();
    private readonly List<Zombie> validIdlePrefabs = new List<Zombie>(); // 대기 군집은 일반 좀비만 (소환자 등 특수 좀비 제외)

    // 예정 생성 대기열. 일반 물량은 남은 수만, 대기 군집 물량은 배치 위치를 가진다. 둘 다 같은 예정 수 안에 있다.
    private int spawnQueue;
    private readonly List<Vector3> idleQueue = new List<Vector3>();
    private int plannedCount;
    private int baseCountThisWave;
    private int spawnedThisWave;
    private int failedThisWave;
    private float preparationEndTime;
    private float nextBatchTime;
    private int batchBudget;
    private int clearObservedFrame = -1;

    private void Awake() {
        wave = startWave - 1;
        if (!ValidateConfiguration()) enabled = false;
    }

    private void OnEnable() {
        if (enemyRegistry != null) enemyRegistry.EnemyDied += HandleEnemyDied;
    }

    private void OnDisable() {
        if (enemyRegistry != null) enemyRegistry.EnemyDied -= HandleEnemyDied;
        StopWaves();
    }

    private void Update() {
        // 게임 오버 상태일때는 남은 생성과 준비를 취소하고 생성하지 않음
        if (GameManager.instance != null && GameManager.instance.isGameover)
        {
            if (Phase != WavePhase.None || QueuedSpawnCount > 0) StopWaves();
            return;
        }

        // Update는 timeScale 0에서도 호출된다. 묶음 예산 소비와 웨이브 전환까지 보존한다.
        if (Time.timeScale <= 0f) return;
        enemyRegistry.PruneDestroyed();

        switch (Phase)
        {
            case WavePhase.None:
            case WavePhase.Completed:
                BeginNextWave();
                break;
            case WavePhase.Preparing:
                // Time.time 기준이므로 일시정지(timeScale 0) 중에는 멈춘다
                if (Time.time >= preparationEndTime) StartSpawning();
                break;
            case WavePhase.Spawning:
                TickSpawning();
                break;
            case WavePhase.Clearing:
                // 대기열 비어 있음 + 예정 생성 완료 + 살아 있는 적 0일 때만 종료하고, 기존처럼 즉시 다음 웨이브로 넘어간다
                if (IsWaveCleared())
                {
                    // 같은 프레임의 보관소/플레이어 파괴가 완료보다 우선하도록 한 프레임 뒤 확정한다.
                    if (clearObservedFrame < 0) clearObservedFrame = Time.frameCount;
                    else if (Time.frameCount > clearObservedFrame)
                    {
                        SetPhase(WavePhase.Completed);
                        BeginNextWave();
                    }
                }
                else clearObservedFrame = -1;
                break;
        }

        // 소환은 FixedUpdate에서 일어나므로 프레임 끝의 대기열 수를 레지스트리에 알려 웨이브 생성에 슬롯 우선권을 준다.
        enemyRegistry.SetWaveQueueDemand(QueuedSpawnCount);

        // UI 갱신
        UpdateUI();
    }

    // 웨이브 정보를 UI로 표시
    private void UpdateUI() {
        UIManager.instance.UpdateWaveText(wave, enemyRegistry.AliveCount);
    }

    private bool IsBigWaveNumber(int number) =>
        bigWaveInterval > 0 && number > 0 && number % bigWaveInterval == 0;

    private bool IsWaveCleared() =>
        QueuedSpawnCount <= 0 && enemyRegistry.PendingCount <= 0 && enemyRegistry.AliveCount <= 0;

    private void BeginNextWave() {
        clearObservedFrame = -1;
        wave++;
        baseCountThisWave = Mathf.RoundToInt(wave * spawnCountPerWave);
        plannedCount = IsBigWave ? Mathf.CeilToInt(baseCountThisWave * bigWaveCountMultiplier) : baseCountThisWave;
        spawnQueue = 0;
        idleQueue.Clear();
        spawnedThisWave = 0;
        failedThisWave = 0;
        batchBudget = 0;

        if (IsBigWave && bigWavePreparationSeconds > 0f)
        {
            preparationEndTime = Time.time + bigWavePreparationSeconds;
            SetPhase(WavePhase.Preparing);
            return;
        }
        StartSpawning();
    }

    private void StartSpawning() {
        // 대기 군집 수 I(w)는 기본 수 B로 구하고 예정 수 안에서 뺀다(추가 적이 아님). 배치하지 못한 몫은 일반 생성으로 남는다.
        idleQueue.Clear();
        if (idleGroupDirector != null && idleGroupDirector.IsReady && validIdlePrefabs.Count > 0)
        {
            int idleCount = Mathf.Min(idleGroupDirector.GetIdleCount(wave, baseCountThisWave), plannedCount);
            idleGroupDirector.PlanPlacements(idleCount, idleQueue);
        }
        spawnQueue = plannedCount - idleQueue.Count;
        nextBatchTime = Time.time;
        SetPhase(WavePhase.Spawning);

        if (!IsBigWave)
        {
            // 일반 웨이브는 기존처럼 같은 프레임에 생성한다. 상한에 막힌 나머지만 대기열에 남는다.
            while (QueuedSpawnCount > 0 && enemyRegistry.HasCapacity) SpawnFromQueue();
        }
        TickSpawning();
    }

    // 빅웨이브: 간격마다 묶음 예산을 다시 채우고 한 프레임에 한 마리씩 생성한다(예산은 누적하지 않음).
    // 일반 웨이브의 상한 대기분도 한 프레임에 한 마리씩 생성한다. 슬롯이 없으면 대기한다.
    private void TickSpawning() {
        if (QueuedSpawnCount > 0)
        {
            if (IsBigWave)
            {
                if (Time.time >= nextBatchTime)
                {
                    batchBudget = bigWaveBatchSize;
                    nextBatchTime = Time.time + bigWaveBatchInterval;
                }
                if (batchBudget > 0 && enemyRegistry.HasCapacity)
                {
                    batchBudget--;
                    SpawnFromQueue();
                }
            }
            else if (enemyRegistry.HasCapacity)
            {
                SpawnFromQueue();
            }
        }

        if (QueuedSpawnCount > 0) return;

        // 예정 수가 있는데 하나도 만들지 못하면 매 프레임 웨이브만 올라가므로 생성을 멈춘다.
        if (plannedCount > 0 && spawnedThisWave == 0)
        {
            Debug.LogError($"ZombieSpawner: 웨이브 {wave}의 좀비를 하나도 생성하지 못해 스포너를 중지합니다.", this);
            enabled = false;
            return;
        }
        if (failedThisWave > 0)
            Debug.LogWarning($"ZombieSpawner: 웨이브 {wave}에서 {plannedCount}마리 중 {failedThisWave}마리 생성에 실패했습니다.", this);
        SetPhase(WavePhase.Clearing);
    }

    // 대기열에서 하나를 꺼내 생성한다(대기 군집 먼저). 슬롯이 있을 때만 호출하며, 그래도 실패하면 그 항목은 버린다.
    private void SpawnFromQueue() {
        bool spawned;
        if (idleQueue.Count > 0)
        {
            Vector3 position = idleQueue[idleQueue.Count - 1];
            idleQueue.RemoveAt(idleQueue.Count - 1);
            // 분할 생성 대기 중 이동한 플레이어·장애물도 반영한다. 실패한 대기 몫은 일반 생성으로 유지한다.
            bool validIdle = idleGroupDirector != null && idleGroupDirector.IsPlacementValid(position);
            spawned = CreateZombie(validIdle ? (Vector3?)position : null, out Zombie zombie);
            if (spawned && validIdle) idleGroupDirector.RegisterMember(zombie);
        }
        else
        {
            spawnQueue--;
            spawned = CreateZombie(null, out _);
        }
        if (spawned) spawnedThisWave++;
        else failedThisWave++;
    }

    // 슬롯을 예약한 뒤 좀비를 생성하고, 실패하면 예약을 반환한다. idlePosition이 있으면 대기 군집 구성원으로 그 위치에 만든다.
    private bool CreateZombie(Vector3? idlePosition, out Zombie created) {
        created = null;
        if (!enemyRegistry.TryReserve(out EnemyRegistry.SpawnReservation reservation)) return false;

        Zombie zombie = null;
        bool committed = false;
        try
        {
            // 일반 생성은 기존과 같은 순서로 난수를 뽑는다: 데이터 → 위치 → 프리팹
            ZombieData zombieData = validDatas[Random.Range(0, validDatas.Count)];
            if (idlePosition.HasValue)
            {
                Quaternion rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                zombie = Instantiate(validIdlePrefabs[Random.Range(0, validIdlePrefabs.Count)], idlePosition.Value, rotation);
            }
            else
            {
                Transform spawnPoint = validSpawnPoints[Random.Range(0, validSpawnPoints.Count)];
                zombie = Instantiate(validPrefabs[Random.Range(0, validPrefabs.Count)], spawnPoint.position, spawnPoint.rotation);
            }
            zombie.Setup(zombieData);
            // 대기 군집은 Idle 역할, 그 밖의 웨이브 좀비는 Wave 역할 (등록 전에 지정해 첫 프레임부터 기본 상태가 맞도록)
            zombie.AssignRole(idlePosition.HasValue ? ZombieRole.Idle : ZombieRole.Wave, baseTarget);
            committed = enemyRegistry.Commit(reservation, zombie);
            if (committed) created = zombie;
        }
        finally
        {
            if (!committed)
            {
                enemyRegistry.Release(reservation);
                if (zombie != null) Destroy(zombie.gameObject);
            }
        }
        return committed;
    }

    // 게임오버·비활성화: 남은 준비·생성을 취소한다. 이미 생성된 적은 그대로 둔다.
    // 단계가 None이 되므로 다시 활성화되면 다음 Update에서 다음 웨이브로 넘어간다(2026-10-07 사용자 결정).
    // 중단된 웨이브의 남은 예정 물량은 이어서 생성하지 않는다. 씬 재로드만 Awake에서 시작 웨이브로 되돌린다.
    private void StopWaves() {
        clearObservedFrame = -1;
        spawnQueue = 0;
        idleQueue.Clear();
        batchBudget = 0;
        preparationEndTime = 0f;
        if (enemyRegistry != null) enemyRegistry.SetWaveQueueDemand(0);
        if (Phase != WavePhase.None) SetPhase(WavePhase.None);
    }

    private void SetPhase(WavePhase phase) {
        Phase = phase;
        PhaseChanged?.Invoke(new WaveStatus(phase, wave, IsBigWave, NextWaveIsBigWave, plannedCount));
    }

    // 소환된 적을 포함해 레지스트리에 등록된 모든 적의 사망 후처리
    private void HandleEnemyDied(Zombie zombie) {
        if (zombie != null) Destroy(zombie.gameObject, corpseDestroyDelay);
        if (GameManager.instance != null) GameManager.instance.AddScore(scorePerKill);
    }

    private bool ValidateConfiguration() {
        bool valid = true;
        if (enemyRegistry == null)
        {
            Debug.LogError("ZombieSpawner: Enemy Registry를 연결하세요.", this);
            valid = false;
        }
        valid &= CollectValid(zombiePrefab, validPrefabs, "Zombie Prefab");
        valid &= CollectValid(zombieDatas, validDatas, "Zombie Datas");
        valid &= CollectValid(spawnPoints, validSpawnPoints, "Spawn Points");
        if (baseTarget == null)
            Debug.LogError("ZombieSpawner: Base Target(보관소)이 연결되지 않아 Wave 좀비는 거점을 공격하지 않고 정지 상태로 대기합니다.", this);
        validIdlePrefabs.Clear();
        foreach (Zombie prefab in validPrefabs)
            if (!(prefab is ISpecialZombie)) validIdlePrefabs.Add(prefab);
        if (idleGroupDirector != null && validIdlePrefabs.Count == 0)
            Debug.LogWarning("ZombieSpawner: 일반 좀비 프리팹이 없어 대기 군집을 배치하지 않습니다.", this);
        return valid;
    }

    // 빈 칸은 오류로 알리고 제외한다. 사용할 항목이 하나도 없으면 false.
    private bool CollectValid<T>(T[] source, List<T> target, string label) where T : UnityEngine.Object {
        target.Clear();
        if (source != null)
        {
            for (int i = 0; i < source.Length; i++)
            {
                if (source[i] != null) target.Add(source[i]);
                else Debug.LogError($"ZombieSpawner: {label}[{i}]이(가) 비어 있어 제외합니다.", this);
            }
        }
        if (target.Count > 0) return true;
        Debug.LogError($"ZombieSpawner: {label}에 유효한 항목을 하나 이상 연결하세요.", this);
        return false;
    }
}
