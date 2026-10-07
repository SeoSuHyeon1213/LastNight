using System;
using System.Collections.Generic;
using UnityEngine;

// 플레이어의 집별 실내 체류 시간으로 활성 거점을 자동 전환한다 (기획 4.2).
// 규칙(2026-10-07 사용자 결정):
// - 활성 거점이 아닌 집에 연속으로 Dwell Seconds Required(기본 180초) 머물면 전환. 집 밖으로 나가거나 다른 집에 들어가면 0부터 다시 센다.
// - 도달 즉시 확정하되 빅웨이브 생성·정리 중이면 클리어까지 보류한다. 보류 중 집을 나가면 후보가 취소된다.
// - 보관소 하나를 새 집으로 옮겨 HP를 유지한다. 집별 방어선·필드 아이템·적은 건드리지 않는다.
// 체류 시간은 Time.deltaTime으로 누적하므로 일시정지 중에는 멈춘다. 게임오버 후에는 전환하지 않는다.
public sealed class BaseRelocationDirector : MonoBehaviour {
    [SerializeField] private BaseHouse[] houses;
    [Tooltip("게임 시작 시 활성 거점. Houses 목록에 포함되어야 한다.")]
    [SerializeField] private BaseHouse initialHouse;
    [SerializeField] private BaseStorage storage;
    [SerializeField] private Transform player;
    [SerializeField] private GameManager gameManager;
    [Tooltip("선택: 빅웨이브 전투 중 전환 보류에 사용. 비우면 보류 없이 즉시 전환한다.")]
    [SerializeField] private ZombieSpawner waveSource;

    [Tooltip("다른 집을 거점으로 확정하기 위한 연속 실내 체류 시간(초). 기본 180초는 플레이 테스트 시작값.")]
    [SerializeField, Min(1f)] private float dwellSecondsRequired = 180f;
    [Tooltip("플레이어 실내 판정 높이. 발 위치가 바닥 콜라이더 경계에 걸리지 않도록 올려서 검사한다.")]
    [SerializeField, Min(0f)] private float playerProbeHeight = 1f;
    [SerializeField, Min(0.05f)] private float anchorNavMeshSnapDistance = 1f;
    [Tooltip("후보 경로 검사에 쓸 좀비 NavMeshAgent. 비우면 기본 Agent Type(Humanoid)으로 검사한다.")]
    [SerializeField] private UnityEngine.AI.NavMeshAgent pathAgentReference;

    public BaseHouse ActiveHouse { get; private set; }
    public BaseHouse CandidateHouse { get; private set; }
    public float CandidateSeconds { get; private set; }
    public float DwellSecondsRequired => dwellSecondsRequired;
    // 기준 시간에 도달했지만 빅웨이브 전투 때문에 확정을 미루는 중
    public bool IsConfirmationDeferred => CandidateHouse != null && CandidateSeconds >= dwellSecondsRequired;

    // 후보가 생기거나 바뀌거나 취소될 때(null). 체류 진행도는 CandidateSeconds로 읽는다.
    public event Action<BaseHouse> CandidateChanged;
    // (이전 거점, 새 거점). 보관소 이동이 끝난 뒤 호출된다.
    public event Action<BaseHouse, BaseHouse> ActiveHouseChanged;

    private readonly List<BaseHouse> validHouses = new List<BaseHouse>();

    private void Awake() {
        if (storage == null || player == null || gameManager == null || initialHouse == null) {
            Debug.LogError("BaseRelocationDirector: Storage, Player, Game Manager, Initial House를 연결하세요.", this);
            enabled = false;
            return;
        }
        CollectValidHouses();
        if (!validHouses.Contains(initialHouse)) {
            Debug.LogError($"BaseRelocationDirector: Initial House '{initialHouse.name}'가 유효한 Houses 목록에 없습니다.", this);
            enabled = false;
            return;
        }
        ActiveHouse = initialHouse;
        // 보관소 위치와 진입로를 활성 집 정의에 맞춘다. 씬 배치와 정의가 어긋나도 한 곳을 기준으로 삼는다.
        storage.Relocate(initialHouse.StorageAnchor, initialHouse.EntryRoutes);
    }

    private void CollectValidHouses() {
        validHouses.Clear();
        var ids = new HashSet<string>();
        if (houses == null) return;
        var filter = new UnityEngine.AI.NavMeshQueryFilter {
            agentTypeID = pathAgentReference != null ? pathAgentReference.agentTypeID : 0,
            areaMask = pathAgentReference != null ? pathAgentReference.areaMask : UnityEngine.AI.NavMesh.AllAreas
        };
        for (int i = 0; i < houses.Length; i++) {
            BaseHouse house = houses[i];
            if (house == null) {
                Debug.LogError($"BaseRelocationDirector: Houses[{i}]가 비어 있어 제외합니다.", this);
                continue;
            }
            if (!house.Validate(filter, anchorNavMeshSnapDistance, out string reason)) {
                Debug.LogWarning($"BaseRelocationDirector: '{house.name}'을(를) 거점 후보에서 제외합니다 ({reason}).", house);
                continue;
            }
            if (!ids.Add(house.HouseId)) {
                Debug.LogError($"BaseRelocationDirector: House Id '{house.HouseId}'가 중복되어 '{house.name}'을(를) 제외합니다.", house);
                continue;
            }
            validHouses.Add(house);
        }
    }

    // 꺼져 있던 동안의 체류는 확인할 수 없으므로 연속 체류 규칙에 따라 후보를 버린다. 활성 거점은 유지한다.
    private void OnDisable() {
        SetCandidate(null);
    }

    private void Update() {
        if (Time.timeScale <= 0f || gameManager.isGameover || storage.dead) return;

        BaseHouse occupied = FindOccupiedHouse(player.position + Vector3.up * playerProbeHeight);
        if (occupied == null || occupied == ActiveHouse) {
            SetCandidate(null);
            return;
        }
        if (occupied != CandidateHouse) {
            SetCandidate(occupied);
            return; // 진입한 프레임은 세지 않는다
        }
        CandidateSeconds = Mathf.Min(dwellSecondsRequired, CandidateSeconds + Time.deltaTime);
        if (CandidateSeconds >= dwellSecondsRequired && CanConfirmNow()) Confirm(CandidateHouse);
    }

    private BaseHouse FindOccupiedHouse(Vector3 probe) {
        // 활성 거점을 먼저 검사해 구역이 겹칠 때 현재 거점을 우선한다.
        if (ActiveHouse != null && ActiveHouse.Contains(probe)) return ActiveHouse;
        foreach (BaseHouse house in validHouses)
            if (house.Contains(probe)) return house;
        return null;
    }

    private bool CanConfirmNow() {
        if (waveSource == null || !waveSource.IsBigWave) return true;
        WavePhase phase = waveSource.Phase;
        return phase != WavePhase.Spawning && phase != WavePhase.Clearing;
    }

    private void SetCandidate(BaseHouse house) {
        if (house == CandidateHouse) return;
        CandidateHouse = house;
        CandidateSeconds = 0f;
        CandidateChanged?.Invoke(house);
    }

    // 활성 ID 확정과 보관소 이동을 한 번에 처리한 뒤 통지한다. 구독자는 새 거점 기준 상태만 보게 된다.
    private void Confirm(BaseHouse house) {
        BaseHouse previous = ActiveHouse;
        ActiveHouse = house;
        storage.Relocate(house.StorageAnchor, house.EntryRoutes);
        CandidateHouse = null;
        CandidateSeconds = 0f;
        CandidateChanged?.Invoke(null);
        ActiveHouseChanged?.Invoke(previous, house);
    }
}
