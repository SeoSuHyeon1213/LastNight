using TMPro;
using UnityEngine;

// 거점 후보 체류 진행도와 거점 전환 알림을 TextMeshProUGUI로 표시한다.
// BaseRelocationDirector의 이벤트와 읽기 전용 상태만 사용하며 전환 규칙은 실행하지 않는다.
// UI 계층(Canvas·텍스트)은 에디터에서 구성하고 Inspector로 연결한다.
public sealed class BaseRelocationNotification : MonoBehaviour {
    [SerializeField] private BaseRelocationDirector director;
    [Tooltip("거점 전환 순간의 일회성 알림")]
    [SerializeField] private TextMeshProUGUI headlineText;
    [Tooltip("후보 집 체류 중 지속 표시")]
    [SerializeField] private TextMeshProUGUI statusText;

    [Header("Timing (초)")]
    [SerializeField, Min(0f)] private float headlineSeconds = 3f;
    [SerializeField, Min(0f)] private float headlineFadeSeconds = 0.5f;

    [Header("Colors")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color deferredColor = new Color(1f, 0.8f, 0.2f);

    [Header("Messages ({0} 집 이름, {1} 남은 초). 한글을 쓰려면 한글 글리프가 있는 TMP 폰트가 필요하다.")]
    [SerializeField] private string settlingFormat = "Settling in {0}: {1}s";
    [SerializeField] private string deferredFormat = "{0}: base moves after BIG WAVE";
    [SerializeField] private string movedFormat = "BASE MOVED: {0}";

    private float headlineEndTime;
    private bool subscribed;
    private int lastStatusSeconds = int.MinValue;
    private bool lastDeferred;
    private BaseHouse lastCandidate;

    private void Awake() {
        if (director == null || headlineText == null || statusText == null) {
            Debug.LogError("BaseRelocationNotification: Director, Headline Text, Status Text를 연결하세요.", this);
            enabled = false;
            return;
        }
        if (headlineText.gameObject == gameObject || statusText.gameObject == gameObject) {
            Debug.LogError("BaseRelocationNotification: 텍스트 오브젝트가 아닌 부모 오브젝트에 부착하세요.", this);
            enabled = false;
            return;
        }
        HideAll();
    }

    private void OnEnable() {
        if (director == null || subscribed) return;
        director.ActiveHouseChanged += HandleActiveHouseChanged;
        subscribed = true;
    }

    private void OnDisable() {
        if (subscribed && director != null) director.ActiveHouseChanged -= HandleActiveHouseChanged;
        subscribed = false;
        HideAll();
    }

    private void HandleActiveHouseChanged(BaseHouse previous, BaseHouse current) {
        if (headlineSeconds <= 0f || current == null) return;
        headlineEndTime = Time.time + headlineSeconds;
        headlineText.text = string.Format(movedFormat, current.DisplayName);
        Color color = headlineText.color;
        color.a = 1f;
        headlineText.color = color;
        headlineText.gameObject.SetActive(true);
    }

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

    // 같은 내용이면 텍스트를 다시 만들지 않는다.
    private void UpdateStatus() {
        BaseHouse candidate = director.CandidateHouse;
        bool deferred = director.IsConfirmationDeferred;
        int seconds = candidate != null
            ? Mathf.CeilToInt(director.DwellSecondsRequired - director.CandidateSeconds) : 0;
        if (candidate == lastCandidate && deferred == lastDeferred && seconds == lastStatusSeconds) return;
        lastCandidate = candidate;
        lastDeferred = deferred;
        lastStatusSeconds = seconds;
        if (candidate == null) {
            statusText.gameObject.SetActive(false);
            return;
        }
        statusText.text = deferred
            ? string.Format(deferredFormat, candidate.DisplayName)
            : string.Format(settlingFormat, candidate.DisplayName, seconds);
        statusText.color = deferred ? deferredColor : normalColor;
        statusText.gameObject.SetActive(true);
    }

    private void HideAll() {
        if (headlineText != null) headlineText.gameObject.SetActive(false);
        if (statusText != null) statusText.gameObject.SetActive(false);
        lastCandidate = null;
        lastDeferred = false;
        lastStatusSeconds = int.MinValue;
        headlineEndTime = 0f;
    }
}
