// 웨이브 진행 단계. 일반 웨이브는 Preparing을 거치지 않는다.
public enum WavePhase {
    None,       // 시작 전 또는 게임오버·비활성화로 중지됨
    Preparing,  // 빅웨이브 전 준비 시간. 적을 생성하지 않는다
    Spawning,   // 예정 생성 대기열을 소모하는 중
    Clearing,   // 대기열이 비었고 살아 있는 적을 정리하는 중
    Completed   // 대기열·예정 생성·살아 있는 적이 모두 0
}

// 단계가 바뀔 때 ZombieSpawner가 알리는 스냅샷. 표시·보급 같은 구독자는 이 값만 읽는다.
public readonly struct WaveStatus {
    public readonly WavePhase Phase;
    public readonly int Wave;
    public readonly bool IsBigWave;
    public readonly bool NextIsBigWave;
    public readonly int PlannedCount;

    public WaveStatus(WavePhase phase, int wave, bool isBigWave, bool nextIsBigWave, int plannedCount) {
        Phase = phase;
        Wave = wave;
        IsBigWave = isBigWave;
        NextIsBigWave = nextIsBigWave;
        PlannedCount = plannedCount;
    }
}
