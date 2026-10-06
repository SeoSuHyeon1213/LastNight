using TMPro;
using UnityEngine;

// 빅웨이브 예고·준비 카운트다운·시작·진행·클리어를 TextMeshProUGUI로 표시한다.
// ZombieSpawner의 단계 이벤트와 읽기 전용 상태만 사용하며 생성·종료 같은 게임 규칙은 실행하지 않는다.
// UI 계층(Canvas·텍스트)은 에디터에서 구성하고 Inspector로 연결한다.
public sealed class BigWaveNotification : MonoBehaviour {
    [SerializeField] private ZombieSpawner waveSpawner;
    [Tooltip("큰 일회성 알림 (준비 진입, BIG WAVE 시작, 클리어)")]
    [SerializeField] private TextMeshProUGUI headlineText;
    [Tooltip("지속 표시 (다음 웨이브 예고, 준비 남은 시간, 진행 중 남은 적)")]
    [SerializeField] private TextMeshProUGUI statusText;
    [Tooltip("선택: 마지막 카운트다운 경고음")]
    [SerializeField] private AudioSource warningAudio;
    [SerializeField] private AudioClip warningClip;

    [Header("Timing (초)")]
    [SerializeField, Min(0f)] private float preparationHeadlineSeconds = 3f;
    [SerializeField, Min(0f)] private float startHeadlineSeconds = 3f;
    [SerializeField, Min(0f)] private float clearHeadlineSeconds = 2f;
    [SerializeField, Min(0f)] private float headlineFadeSeconds = 0.5f;
    [SerializeField, Min(0)] private int countdownWarningSeconds = 5;

    [Header("Colors")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color warningColor = new Color(1f, 0.25f, 0.2f);

    [Header("Messages ({0} 자리에 숫자). 한글을 쓰려면 한글 글리프가 있는 TMP 폰트가 필요하다.")]
    [SerializeField] private string upcomingFormat = "다음 웨이브: BIG WAVE";
    [SerializeField] private string preparationHeadlineFormat = "빅웨이브까지 {0}초 — 전투를 준비하세요";
    [SerializeField] private string preparationStatusFormat = "BIG WAVE까지 {0}";
    [SerializeField] private string countdownFormat = "{0}…";
    [SerializeField] private string startHeadlineFormat = "BIG WAVE — {0}";
    [SerializeField] private string combatStatusFormat = "BIG WAVE   남은 적 {0}";
    [SerializeField] private string clearHeadlineFormat = "BIG WAVE CLEAR!";

    private float headlineEndTime;
    private bool subscribed;
    private int lastStatusNumber = int.MinValue;
    private int lastStatusKind = -1;
    private int lastWarningSecond = -1;

    private void Awake() {
        if (waveSpawner == null || headlineText == null || statusText == null) {
            Debug.LogError("BigWaveNotification: Wave Spawner, Headline Text, Status Text를 연결하세요.", this);
            enabled = false;
            return;
        }
        // 텍스트를 숨길 때 이 컴포넌트까지 꺼지지 않도록 부모 오브젝트에 둔다.
        if (headlineText.gameObject == gameObject || statusText.gameObject == gameObject) {
            Debug.LogError("BigWaveNotification: 텍스트 오브젝트가 아닌 부모 오브젝트에 부착하세요.", this);
            enabled = false;
            return;
        }
        HideAll();
    }

    private void OnEnable() {
        if (waveSpawner == null || subscribed) return;
        waveSpawner.PhaseChanged += HandlePhaseChanged;
        subscribed = true;
    }

    private void OnDisable() {
        if (subscribed && waveSpawner != null) waveSpawner.PhaseChanged -= HandlePhaseChanged;
        subscribed = false;
        HideAll();
    }

    private void HandlePhaseChanged(WaveStatus status) {
        switch (status.Phase) {
            case WavePhase.Preparing:
                lastWarningSecond = -1;
                ShowHeadline(string.Format(preparationHeadlineFormat,
                    Mathf.CeilToInt(waveSpawner.PreparationTimeRemaining)), preparationHeadlineSeconds);
                break;
            case WavePhase.Spawning:
                if (status.IsBigWave) ShowHeadline(string.Format(startHeadlineFormat, status.Wave), startHeadlineSeconds);
                break;
            case WavePhase.Completed:
                if (status.IsBigWave) ShowHeadline(clearHeadlineFormat, clearHeadlineSeconds);
                break;
            case WavePhase.None:
                HideAll();
                break;
        }
    }

    // 지속 표시와 일회성 알림의 남은 시간만 갱신한다. Time.time 기준이라 일시정지 중에는 멈춘다.
    private void Update() {
        UpdateHeadline();
        UpdateStatus();
    }

    private void UpdateHeadline() {
        if (!headlineText.gameObject.activeSelf) return;
        float remaining = headlineEndTime - Time.time;
        if (remaining <= 0f) {
            headlineText.gameObject.SetActive(false);
            return;
        }
        Color color = headlineText.color;
        color.a = headlineFadeSeconds > 0f ? Mathf.Clamp01(remaining / headlineFadeSeconds) : 1f;
        headlineText.color = color;
    }

    private void UpdateStatus() {
        WavePhase phase = waveSpawner.Phase;
        bool active = phase == WavePhase.Spawning || phase == WavePhase.Clearing;

        if (phase == WavePhase.Preparing) {
            int seconds = Mathf.CeilToInt(waveSpawner.PreparationTimeRemaining);
            bool warning = seconds <= countdownWarningSeconds;
            SetStatus(warning ? 2 : 1, seconds,
                warning ? string.Format(countdownFormat, seconds) : string.Format(preparationStatusFormat, seconds),
                warning ? warningColor : normalColor);
            if (warning && seconds > 0 && seconds != lastWarningSecond) {
                lastWarningSecond = seconds;
                if (warningAudio != null && warningClip != null) warningAudio.PlayOneShot(warningClip);
            }
        } else if (active && waveSpawner.IsBigWave) {
            int remaining = waveSpawner.RemainingEnemyCount;
            SetStatus(3, remaining, string.Format(combatStatusFormat, remaining), normalColor);
        } else if (active && waveSpawner.NextWaveIsBigWave) {
            SetStatus(4, 0, upcomingFormat, normalColor);
        } else {
            SetStatus(0, 0, null, normalColor);
        }
    }

    // 같은 내용이면 텍스트를 다시 만들지 않는다.
    private void SetStatus(int kind, int number, string text, Color color) {
        if (kind == lastStatusKind && number == lastStatusNumber) return;
        lastStatusKind = kind;
        lastStatusNumber = number;
        if (kind == 0) {
            statusText.gameObject.SetActive(false);
            return;
        }
        statusText.text = text;
        statusText.color = color;
        statusText.gameObject.SetActive(true);
    }

    private void ShowHeadline(string text, float seconds) {
        if (seconds <= 0f) return;
        headlineEndTime = Time.time + seconds;
        headlineText.text = text;
        Color color = headlineText.color;
        color.a = 1f;
        headlineText.color = color;
        headlineText.gameObject.SetActive(true);
    }

    private void HideAll() {
        if (headlineText != null) headlineText.gameObject.SetActive(false);
        if (statusText != null) statusText.gameObject.SetActive(false);
        lastStatusKind = -1;
        lastStatusNumber = int.MinValue;
        headlineEndTime = 0f;
    }
}
