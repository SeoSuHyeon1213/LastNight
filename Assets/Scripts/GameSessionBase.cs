using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// PC·모바일 게임 씬이 공유하는 세션 기능: 일시정지(timeScale), 일시정지·설정 창, 시점 감도, 씬 이동.
// 다른 스크립트는 instance로 일시정지 상태와 감도를 읽는다. 장치별 입력과 화면 설정은 하위 클래스가 맡는다.
[DisallowMultipleComponent]
public abstract class GameSessionBase : MonoBehaviour {
    public static GameSessionBase instance { get; private set; }
    public bool IsPaused { get; private set; }
    // 키 재설정 중에는 게임 입력을 막는다. 재설정이 없는 장치는 항상 false다.
    public virtual bool IsRebinding => false;
    public GameObject ActiveMenuPanel => IsSettingsOpen
        ? settingsPanel : pausePanel != null && pausePanel.activeInHierarchy ? pausePanel : null;
    protected bool IsSettingsOpen => settingsPanel != null && settingsPanel.activeInHierarchy;

    // 필드 이름은 기존 GameSessionManager 직렬화 데이터를 유지하기 위해 바꾸지 않는다.
    [Header("Pause Menu")]
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject settingsPanel;

    [Header("Look Sensitivity")]
    [SerializeField] private Slider mouseSensitivityXSlider;
    [SerializeField] private Slider mouseSensitivityYSlider;
    [SerializeField] private Text mouseSensitivityXText;
    [SerializeField] private Text mouseSensitivityYText;
    private const float MinSensitivity = 0.1f;
    private const float MaxSensitivity = 3f;
    private const string SensitivityXKey = "MouseSensitivity.X";
    private const string SensitivityYKey = "MouseSensitivity.Y";
    private bool sensitivityDirty;
    public float MouseSensitivityX { get; private set; } = 1f;
    public float MouseSensitivityY { get; private set; } = 1f;

    private void Awake() {
        if (instance != null && instance != this) {
            Destroy(this);
            return;
        }

        instance = this;
        ValidateUiReferences();
        InitializeDevice();
        InitializeMouseSensitivity();
        RefreshSettingsLabels();
        SetPaused(false);
    }

    private void Update() {
        if (ReadPauseToggle() && (GameManager.instance == null || !GameManager.instance.isGameover))
            SetPaused(!IsPaused);
    }

    private void OnDestroy() {
        if (instance != this)
            return;

        instance = null;
        if (mouseSensitivityXSlider != null)
            mouseSensitivityXSlider.onValueChanged.RemoveListener(SetMouseSensitivityX);
        if (mouseSensitivityYSlider != null)
            mouseSensitivityYSlider.onValueChanged.RemoveListener(SetMouseSensitivityY);
        SaveMouseSensitivity();
        Time.timeScale = 1f;
    }

    // 저장된 바인딩·화면 설정 등 장치별 상태를 불러온다. Awake에서 한 번 호출된다.
    protected virtual void InitializeDevice() { }

    // 이번 프레임에 일시정지를 전환해야 하면 true. Update마다 호출된다.
    protected abstract bool ReadPauseToggle();

    // 일시정지 전환이나 설정 창 닫기로 메뉴 상태가 초기화될 때 호출된다.
    protected virtual void OnMenuReset() { }

    public virtual void ApplyCursorMode() { }

    public void SetPaused(bool paused) {
        if (!paused) SaveMouseSensitivity();
        IsPaused = paused;
        Time.timeScale = paused ? 0f : 1f;

        if (pausePanel != null)
            pausePanel.SetActive(paused);
        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        OnMenuReset();
        ApplyCursorMode();
    }

    // 게임오버 후에는 일시정지 창을 열지 않는다. 모바일 HUD의 일시정지 버튼에 연결한다.
    public void PauseGame() {
        if (GameManager.instance != null && GameManager.instance.isGameover) return;
        SetPaused(true);
    }

    public void ResumeGame() {
        SetPaused(false);
    }

    public void OpenSettings() {
        if (!IsPaused)
            return;

        if (pausePanel != null)
            pausePanel.SetActive(false);
        if (settingsPanel != null)
            settingsPanel.SetActive(true);

        RefreshSettingsLabels();
    }

    public void CloseSettings() {
        SaveMouseSensitivity();
        OnMenuReset();

        if (settingsPanel != null)
            settingsPanel.SetActive(false);
        if (pausePanel != null)
            pausePanel.SetActive(true);
    }

    public void RestartScene() {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public virtual void LoadMainScene() {
        if (SceneManager.sceneCountInBuildSettings <= 0) {
            Debug.LogError("Main scene could not be loaded because build index 0 is not configured.", this);
            return;
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene(0);
    }

    public void QuitGame() {
        Time.timeScale = 1f;
        PlayerPrefs.Save();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    protected virtual void RefreshSettingsLabels() {
        RefreshMouseSensitivityUi();
    }

    private void InitializeMouseSensitivity() {
        MouseSensitivityX = ClampSensitivity(PlayerPrefs.GetFloat(SensitivityXKey, 1f));
        MouseSensitivityY = ClampSensitivity(PlayerPrefs.GetFloat(SensitivityYKey, 1f));
        ConfigureSensitivitySlider(mouseSensitivityXSlider, MouseSensitivityX);
        ConfigureSensitivitySlider(mouseSensitivityYSlider, MouseSensitivityY);
        if (mouseSensitivityXSlider != null)
            mouseSensitivityXSlider.onValueChanged.AddListener(SetMouseSensitivityX);
        if (mouseSensitivityYSlider != null)
            mouseSensitivityYSlider.onValueChanged.AddListener(SetMouseSensitivityY);
        if (mouseSensitivityXSlider == null || mouseSensitivityYSlider == null)
            Debug.LogWarning($"{GetType().Name}: X/Y 감도 Slider를 Inspector에 연결하세요.", this);
    }

    private static float ClampSensitivity(float value) {
        return float.IsNaN(value) || float.IsInfinity(value) ? 1f : Mathf.Clamp(value, MinSensitivity, MaxSensitivity);
    }

    private static void ConfigureSensitivitySlider(Slider slider, float value) {
        if (slider == null) return;
        slider.minValue = MinSensitivity;
        slider.maxValue = MaxSensitivity;
        slider.wholeNumbers = false;
        slider.SetValueWithoutNotify(value);
    }

    public void SetMouseSensitivityX(float value) {
        MouseSensitivityX = ClampSensitivity(value);
        PlayerPrefs.SetFloat(SensitivityXKey, MouseSensitivityX);
        sensitivityDirty = true;
        RefreshMouseSensitivityUi();
    }

    public void SetMouseSensitivityY(float value) {
        MouseSensitivityY = ClampSensitivity(value);
        PlayerPrefs.SetFloat(SensitivityYKey, MouseSensitivityY);
        sensitivityDirty = true;
        RefreshMouseSensitivityUi();
    }

    private void RefreshMouseSensitivityUi() {
        if (mouseSensitivityXSlider != null) mouseSensitivityXSlider.SetValueWithoutNotify(MouseSensitivityX);
        if (mouseSensitivityYSlider != null) mouseSensitivityYSlider.SetValueWithoutNotify(MouseSensitivityY);
        if (mouseSensitivityXText != null) mouseSensitivityXText.text = $"X  {MouseSensitivityX:0.00}x";
        if (mouseSensitivityYText != null) mouseSensitivityYText.text = $"Y  {MouseSensitivityY:0.00}x";
    }

    private void SaveMouseSensitivity() {
        if (!sensitivityDirty) return;
        PlayerPrefs.Save();
        sensitivityDirty = false;
    }

    private void OnApplicationFocus(bool focused) {
        if (instance == this && !focused) SaveMouseSensitivity();
    }

    private void OnApplicationQuit() {
        if (instance == this) SaveMouseSensitivity();
    }

    protected virtual void ValidateUiReferences() {
        if (pausePanel == null || settingsPanel == null) {
            Debug.LogError(
                $"{GetType().Name} on '{name}' requires Pause Panel and Settings Panel references.",
                this);
        }
    }
}
