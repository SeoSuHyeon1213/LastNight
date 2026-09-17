using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class GameSessionManager : MonoBehaviour {
    private enum BindingAction { Forward, Backward, Left, Right, Run, Fire, Reload, Pause }

    public static GameSessionManager instance { get; private set; }
    public bool IsPaused { get; private set; }
    public bool IsRebinding => rebindingAction.HasValue;

    [Header("Pause Menu")]
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject settingsPanel;

    [Header("Settings Labels")]
    [SerializeField] private Text rebindingText;
    [SerializeField] private Text forwardBindingText;
    [SerializeField] private Text backwardBindingText;
    [SerializeField] private Text leftBindingText;
    [SerializeField] private Text rightBindingText;
    [SerializeField] private Text runBindingText;
    [SerializeField] private Text fireBindingText;
    [SerializeField] private Text reloadBindingText;
    [SerializeField] private Text pauseBindingText;

    [Header("Display Mode Indicators")]
    [SerializeField] private GameObject fullscreenIndicator;
    [SerializeField] private GameObject windowedIndicator;

    [Header("Default Key Bindings")]
    [SerializeField] private KeyCode forwardKey = KeyCode.W;
    [SerializeField] private KeyCode backwardKey = KeyCode.S;
    [SerializeField] private KeyCode leftKey = KeyCode.A;
    [SerializeField] private KeyCode rightKey = KeyCode.D;
    [SerializeField] private KeyCode runKey = KeyCode.LeftShift;
    [SerializeField] private KeyCode fireKey = KeyCode.Mouse0;
    [SerializeField] private KeyCode reloadKey = KeyCode.R;
    [SerializeField] private KeyCode pauseKey = KeyCode.Escape;

    private BindingAction? rebindingAction;
    private bool fullscreenMode;

    public float MoveInput => GetAxis(forwardKey, backwardKey);
    public float StrafeInput => GetAxis(rightKey, leftKey);
    public bool IsRunning => Input.GetKey(runKey);
    public bool FireHeld => Input.GetKey(fireKey);
    public bool ReloadPressed => Input.GetKeyDown(reloadKey);

    private void Awake() {
        if (instance != null && instance != this) {
            Destroy(this);
            return;
        }

        instance = this;
        ValidateUiReferences();
        LoadBindings();
        fullscreenMode = PlayerPrefs.GetInt("DisplayFullscreen", 1) == 1;
        ApplyDisplayMode();
        RefreshSettingsLabels();
        SetPaused(false);
    }

    private void Update() {
        if (rebindingAction.HasValue) {
            CaptureRebindingKey();
            return;
        }

        if (Input.GetKeyDown(pauseKey) && (GameManager.instance == null || !GameManager.instance.isGameover))
            SetPaused(!IsPaused);
    }

    private void OnDestroy() {
        if (instance != this)
            return;

        instance = null;
        Time.timeScale = 1f;
    }

    public void SetPaused(bool paused) {
        IsPaused = paused;
        rebindingAction = null;
        Time.timeScale = paused ? 0f : 1f;

        if (pausePanel != null)
            pausePanel.SetActive(paused);
        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        SetRebindingMessage("조작 설정");
        ApplyCursorMode();
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
        rebindingAction = null;
        SetRebindingMessage("조작 설정");

        if (settingsPanel != null)
            settingsPanel.SetActive(false);
        if (pausePanel != null)
            pausePanel.SetActive(true);
    }

    public void RestartScene() {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void LoadMainScene() {
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

    public void SetFullscreenMode() {
        SetDisplayMode(true);
    }

    public void SetWindowedMode() {
        SetDisplayMode(false);
    }

    private void SetDisplayMode(bool useFullscreen) {
        fullscreenMode = useFullscreen;
        PlayerPrefs.SetInt("DisplayFullscreen", fullscreenMode ? 1 : 0);
        PlayerPrefs.Save();
        ApplyDisplayMode();
        RefreshDisplayModeIndicators();
    }

    public void RebindForward() { BeginRebinding(BindingAction.Forward); }
    public void RebindBackward() { BeginRebinding(BindingAction.Backward); }
    public void RebindLeft() { BeginRebinding(BindingAction.Left); }
    public void RebindRight() { BeginRebinding(BindingAction.Right); }
    public void RebindRun() { BeginRebinding(BindingAction.Run); }
    public void RebindFire() { BeginRebinding(BindingAction.Fire); }
    public void RebindReload() { BeginRebinding(BindingAction.Reload); }
    public void RebindPause() { BeginRebinding(BindingAction.Pause); }

    private void BeginRebinding(BindingAction action) {
        rebindingAction = action;
        SetRebindingMessage($"{GetLabel(action)}: 새 키를 누르세요");
    }

    private void CaptureRebindingKey() {
        foreach (KeyCode key in Enum.GetValues(typeof(KeyCode))) {
            if (key == KeyCode.None || !Input.GetKeyDown(key))
                continue;

            SetBinding(rebindingAction.Value, key);
            rebindingAction = null;
            SetRebindingMessage("조작 설정");
            RefreshBindingTexts();
            SaveBindings();
            return;
        }
    }

    private void ApplyDisplayMode() {
        Screen.fullScreenMode = fullscreenMode
            ? FullScreenMode.FullScreenWindow
            : FullScreenMode.Windowed;
        ApplyCursorMode();
    }

    private void ApplyCursorMode() {
        bool releaseCursor = IsPaused || !fullscreenMode;
        Cursor.lockState = releaseCursor ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = releaseCursor;
    }

    private void RefreshSettingsLabels() {
        RefreshBindingTexts();
        RefreshDisplayModeIndicators();
    }

    private void RefreshBindingTexts() {
        SetBindingText(forwardBindingText, BindingAction.Forward);
        SetBindingText(backwardBindingText, BindingAction.Backward);
        SetBindingText(leftBindingText, BindingAction.Left);
        SetBindingText(rightBindingText, BindingAction.Right);
        SetBindingText(runBindingText, BindingAction.Run);
        SetBindingText(fireBindingText, BindingAction.Fire);
        SetBindingText(reloadBindingText, BindingAction.Reload);
        SetBindingText(pauseBindingText, BindingAction.Pause);
    }

    private void SetBindingText(Text target, BindingAction action) {
        if (target != null)
            target.text = $"{GetLabel(action)}: {GetBinding(action)}";
    }

    private void RefreshDisplayModeIndicators() {
        if (fullscreenIndicator != null)
            fullscreenIndicator.SetActive(fullscreenMode);
        if (windowedIndicator != null)
            windowedIndicator.SetActive(!fullscreenMode);
    }

    private void SetRebindingMessage(string message) {
        if (rebindingText != null)
            rebindingText.text = message;
    }

    private float GetAxis(KeyCode positive, KeyCode negative) {
        return (Input.GetKey(positive) ? 1f : 0f) - (Input.GetKey(negative) ? 1f : 0f);
    }

    private KeyCode GetBinding(BindingAction action) {
        switch (action) {
            case BindingAction.Forward: return forwardKey;
            case BindingAction.Backward: return backwardKey;
            case BindingAction.Left: return leftKey;
            case BindingAction.Right: return rightKey;
            case BindingAction.Run: return runKey;
            case BindingAction.Fire: return fireKey;
            case BindingAction.Reload: return reloadKey;
            default: return pauseKey;
        }
    }

    private void SetBinding(BindingAction action, KeyCode key) {
        switch (action) {
            case BindingAction.Forward: forwardKey = key; break;
            case BindingAction.Backward: backwardKey = key; break;
            case BindingAction.Left: leftKey = key; break;
            case BindingAction.Right: rightKey = key; break;
            case BindingAction.Run: runKey = key; break;
            case BindingAction.Fire: fireKey = key; break;
            case BindingAction.Reload: reloadKey = key; break;
            case BindingAction.Pause: pauseKey = key; break;
        }
    }

    private string GetLabel(BindingAction action) {
        switch (action) {
            case BindingAction.Forward: return "전진";
            case BindingAction.Backward: return "후진";
            case BindingAction.Left: return "왼쪽 이동";
            case BindingAction.Right: return "오른쪽 이동";
            case BindingAction.Run: return "달리기";
            case BindingAction.Fire: return "발사";
            case BindingAction.Reload: return "재장전";
            default: return "일시정지";
        }
    }

    private void LoadBindings() {
        foreach (BindingAction action in Enum.GetValues(typeof(BindingAction))) {
            string saved = PlayerPrefs.GetString($"KeyBinding.{action}", GetBinding(action).ToString());
            if (Enum.TryParse(saved, out KeyCode key))
                SetBinding(action, key);
        }
    }

    private void SaveBindings() {
        foreach (BindingAction action in Enum.GetValues(typeof(BindingAction)))
            PlayerPrefs.SetString($"KeyBinding.{action}", GetBinding(action).ToString());
        PlayerPrefs.Save();
    }

    private void ValidateUiReferences() {
        if (pausePanel == null || settingsPanel == null) {
            Debug.LogError(
                $"{nameof(GameSessionManager)} on '{name}' requires Pause Panel and Settings Panel references.",
                this);
        }

        if (rebindingText == null
            || forwardBindingText == null || backwardBindingText == null
            || leftBindingText == null || rightBindingText == null
            || runBindingText == null || fireBindingText == null
            || reloadBindingText == null || pauseBindingText == null) {
            Debug.LogWarning(
                $"{nameof(GameSessionManager)} on '{name}' has an unassigned settings Text reference.",
                this);
        }

        if (fullscreenIndicator == null || windowedIndicator == null) {
            Debug.LogWarning(
                $"{nameof(GameSessionManager)} on '{name}' requires both display mode indicator references.",
                this);
        }
    }
}
