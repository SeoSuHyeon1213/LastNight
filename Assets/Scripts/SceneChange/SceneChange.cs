using System;
using UnityEngine;
using UnityEngine.SceneManagement;

// Intro 메뉴용. Game 씬의 일시정지/세션 상태를 변경하지 않는다.
public class SceneChange : MonoBehaviour {
    private enum BindingAction { Forward, Backward, Left, Right, Run, Fire, Reload, Pause }
    private static readonly KeyCode[] DefaultKeys = {
        KeyCode.W, KeyCode.S, KeyCode.A, KeyCode.D,
        KeyCode.LeftShift, KeyCode.Mouse0, KeyCode.R, KeyCode.Escape
    };
    private static readonly string[] BindingLabels = {
        "Forward", "Backward", "Left", "Right", "Run", "Fire", "Reload", "Pause"
    };
    private static readonly KeyCode[] CaptureKeys = (KeyCode[])Enum.GetValues(typeof(KeyCode));
    private const string SensitivityXKey = "MouseSensitivity.X";
    private const string SensitivityYKey = "MouseSensitivity.Y";

    [Header("Intro Windows")]
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject quitConfirmationPanel;
    [Tooltip("선택 사항. Settings와 Quit 창을 포함하지 않는 메인 버튼 그룹을 연결하세요.")]
    [SerializeField] private GameObject mainMenuPanel;

    [Header("Key Binding Labels (기존 UI Text)")]
    [SerializeField] private UnityEngine.UI.Text rebindingText;
    [SerializeField] private UnityEngine.UI.Text forwardBindingText;
    [SerializeField] private UnityEngine.UI.Text backwardBindingText;
    [SerializeField] private UnityEngine.UI.Text leftBindingText;
    [SerializeField] private UnityEngine.UI.Text rightBindingText;
    [SerializeField] private UnityEngine.UI.Text runBindingText;
    [SerializeField] private UnityEngine.UI.Text fireBindingText;
    [SerializeField] private UnityEngine.UI.Text reloadBindingText;
    [SerializeField] private UnityEngine.UI.Text pauseBindingText;

    [Tooltip("키 버튼의 TMP 텍스트: Forward, Backward, Left, Right, Run, Fire, Reload, Pause 순서")]
    [SerializeField] private TMPro.TMP_Text[] bindingButtonTexts = new TMPro.TMP_Text[8];

    [Header("Display Mode Indicators")]
    [SerializeField] private GameObject fullscreenIndicator;
    [SerializeField] private GameObject windowedIndicator;

    [Header("Mouse Sensitivity (선택 사항)")]
    [SerializeField] private UnityEngine.UI.Slider mouseSensitivityXSlider;
    [SerializeField] private UnityEngine.UI.Slider mouseSensitivityYSlider;
    [SerializeField] private UnityEngine.UI.Text mouseSensitivityXText;
    [SerializeField] private UnityEngine.UI.Text mouseSensitivityYText;

    private readonly KeyCode[] bindings = new KeyCode[DefaultKeys.Length];
    private BindingAction? rebindingAction;
    private int rebindingStartFrame;
    private bool fullscreenMode;
    private bool settingsDirty;
    private bool loadingScene;
    public bool IsRebinding => rebindingAction.HasValue;
    public float MouseSensitivityX { get; private set; } = 1f;
    public float MouseSensitivityY { get; private set; } = 1f;

    private void Awake() {
        LoadSettings();
        ConfigureSlider(mouseSensitivityXSlider, MouseSensitivityX);
        ConfigureSlider(mouseSensitivityYSlider, MouseSensitivityY);
        if (mouseSensitivityXSlider != null)
            mouseSensitivityXSlider.onValueChanged.AddListener(SetMouseSensitivityX);
        if (mouseSensitivityYSlider != null)
            mouseSensitivityYSlider.onValueChanged.AddListener(SetMouseSensitivityY);
        // 기존 시작 버튼에만 붙어 있는 SceneChange도 계속 사용할 수 있다.
        if (settingsPanel != null || quitConfirmationPanel != null) {
            if (settingsPanel != null && !RequirePanel(settingsPanel, "Settings Panel")) return;
            if (quitConfirmationPanel != null && !RequirePanel(quitConfirmationPanel, "Quit Confirmation Panel")) return;
            ShowMainMenu();
            ApplyDisplayMode();
            RefreshSettingsLabels();
        }
    }

    private void Update() {
        if (IsRebinding) {
            if (settingsPanel == null || !settingsPanel.activeInHierarchy) CancelRebinding();
            else if (Time.frameCount > rebindingStartFrame) CaptureRebindingKey();
            return;
        }
        if (!Input.GetKeyDown(KeyCode.Escape)) return;
        if (quitConfirmationPanel != null && quitConfirmationPanel.activeInHierarchy) CancelQuit();
        else if (settingsPanel != null && settingsPanel.activeInHierarchy) CloseSettings();
    }

    private void OnDisable() {
        CancelRebinding();
        SaveSettings();
    }

    private void OnDestroy() {
        if (mouseSensitivityXSlider != null)
            mouseSensitivityXSlider.onValueChanged.RemoveListener(SetMouseSensitivityX);
        if (mouseSensitivityYSlider != null)
            mouseSensitivityYSlider.onValueChanged.RemoveListener(SetMouseSensitivityY);
    }

    private void OnApplicationFocus(bool focused) {
        if (!focused) { CancelRebinding(); SaveSettings(); }
    }
    private void OnApplicationQuit() { SaveSettings(); }

    public void SceneChangeToZero() { LoadScene(0); }
    public void SceneChangeToOne() { LoadScene(1); }

    private void LoadScene(int buildIndex) {
        if (loadingScene || IsRebinding) return;
        if (buildIndex >= SceneManager.sceneCountInBuildSettings) {
            Debug.LogError($"SceneChange: Build Settings의 {buildIndex}번 씬을 연결하세요.", this);
            return;
        }
        SaveSettings();
        Time.timeScale = 1f;
        loadingScene = true;
        SceneManager.LoadScene(buildIndex);
    }

    public void OpenSettings() {
        if (!RequirePanel(settingsPanel, "Settings Panel")) return;
        CancelRebinding();
        LoadSettings();
        if (quitConfirmationPanel != null) quitConfirmationPanel.SetActive(false);
        SetMainMenuVisible(false);
        settingsPanel.SetActive(true);
        RefreshSettingsLabels();
        ReleaseCursor();
    }

    public void CloseSettings() { ShowMainMenu(); }

    public void OpenQuitConfirmation() {
        if (!RequirePanel(quitConfirmationPanel, "Quit Confirmation Panel")) return;
        CancelRebinding();
        SaveSettings();
        if (settingsPanel != null) settingsPanel.SetActive(false);
        SetMainMenuVisible(false);
        quitConfirmationPanel.SetActive(true);
        ReleaseCursor();
    }

    public void CancelQuit() { ShowMainMenu(); }

    public void QuitGame() {
        // 메인 메뉴의 나가기 버튼은 OpenQuitConfirmation에 연결한다.
        if (quitConfirmationPanel == null || !quitConfirmationPanel.activeInHierarchy) {
            Debug.LogWarning("SceneChange: 종료 확인창을 연 뒤 확인 버튼으로 종료하세요.", this);
            return;
        }
        SaveSettings();
        PlayerPrefs.Save();
        Time.timeScale = 1f;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void ShowMainMenu() {
        CancelRebinding();
        SaveSettings();
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (quitConfirmationPanel != null) quitConfirmationPanel.SetActive(false);
        SetMainMenuVisible(true);
        ReleaseCursor();
    }

    private void SetMainMenuVisible(bool visible) {
        if (mainMenuPanel == null) return;
        if (!visible && (mainMenuPanel == gameObject || transform.IsChildOf(mainMenuPanel.transform)
            || (settingsPanel != null && settingsPanel.transform.IsChildOf(mainMenuPanel.transform))
            || (quitConfirmationPanel != null && quitConfirmationPanel.transform.IsChildOf(mainMenuPanel.transform)))) {
            Debug.LogError("SceneChange: Main Menu Panel은 컨트롤러와 설정/종료 창을 포함하지 않는 버튼 그룹이어야 합니다.", this);
            return;
        }
        mainMenuPanel.SetActive(visible);
    }

    private bool RequirePanel(GameObject panel, string field) {
        if (panel != null && panel != gameObject && !transform.IsChildOf(panel.transform)) return true;
        Debug.LogError($"SceneChange: {field}을 연결하고 SceneChange를 창 밖의 항상 활성인 오브젝트에 배치하세요.", this);
        return false;
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
        if (settingsPanel == null || !settingsPanel.activeInHierarchy) return;
        rebindingAction = action;
        rebindingStartFrame = Time.frameCount;
        SetRebindingMessage($"{BindingLabels[(int)action]}: PRESS A KEY");
    }

    private void CaptureRebindingKey() {
        foreach (KeyCode key in CaptureKeys) {
            if (key == KeyCode.None || !Input.GetKeyDown(key)) continue;
            ApplyBinding(key);
            return;
        }
    }

    private void ApplyBinding(KeyCode key) {
        if (!rebindingAction.HasValue || key == KeyCode.None) return;
        var action = rebindingAction.Value;
        bindings[(int)action] = key;
        PlayerPrefs.SetString("KeyBinding." + action, key.ToString());
        // 기존 입력 경로 이전 데이터도 같은 키로 갱신한다.
        if (LegacyKeyBindingMigration.TryConvert(key.ToString(), out string path))
            PlayerPrefs.SetString(LegacyKeyBindingMigration.PathPrefix + action, path);
        else PlayerPrefs.DeleteKey(LegacyKeyBindingMigration.PathPrefix + action);
        settingsDirty = true;
        CancelRebinding();
        RefreshSettingsLabels();
        SaveSettings();
    }

    public void CancelRebinding() {
        rebindingAction = null;
        SetRebindingMessage("Controls");
    }

    public void SetFullscreenMode() { SetDisplayMode(true); }
    public void SetWindowedMode() { SetDisplayMode(false); }
    private void SetDisplayMode(bool fullscreen) {
        fullscreenMode = fullscreen;
        PlayerPrefs.SetInt("DisplayFullscreen", fullscreen ? 1 : 0);
        settingsDirty = true;
        ApplyDisplayMode();
        RefreshSettingsLabels();
        SaveSettings();
    }
    private void ApplyDisplayMode() {
        Screen.fullScreenMode = fullscreenMode ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
        ReleaseCursor();
    }
    private static void ReleaseCursor() {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void LoadSettings() {
        foreach (BindingAction action in Enum.GetValues(typeof(BindingAction))) {
            string saved = PlayerPrefs.GetString("KeyBinding." + action, DefaultKeys[(int)action].ToString());
            bindings[(int)action] = Enum.TryParse(saved, out KeyCode key) && key != KeyCode.None
                && Enum.IsDefined(typeof(KeyCode), key) ? key : DefaultKeys[(int)action];
        }
        MouseSensitivityX = ClampSensitivity(PlayerPrefs.GetFloat(SensitivityXKey, 1f));
        MouseSensitivityY = ClampSensitivity(PlayerPrefs.GetFloat(SensitivityYKey, 1f));
        fullscreenMode = PlayerPrefs.GetInt("DisplayFullscreen", 1) == 1;
    }
    private void SaveSettings() {
        if (!settingsDirty) return;
        PlayerPrefs.Save();
        settingsDirty = false;
    }
    private static float ClampSensitivity(float value) {
        return float.IsNaN(value) || float.IsInfinity(value) ? 1f : Mathf.Clamp(value, 0.1f, 3f);
    }
    private static void ConfigureSlider(UnityEngine.UI.Slider slider, float value) {
        if (slider == null) return;
        slider.minValue = 0.1f;
        slider.maxValue = 3f;
        slider.wholeNumbers = false;
        slider.SetValueWithoutNotify(value);
    }
    public void SetMouseSensitivityX(float value) {
        MouseSensitivityX = ClampSensitivity(value);
        PlayerPrefs.SetFloat(SensitivityXKey, MouseSensitivityX);
        settingsDirty = true;
        RefreshSettingsLabels();
    }
    public void SetMouseSensitivityY(float value) {
        MouseSensitivityY = ClampSensitivity(value);
        PlayerPrefs.SetFloat(SensitivityYKey, MouseSensitivityY);
        settingsDirty = true;
        RefreshSettingsLabels();
    }
    private void SetRebindingMessage(string message) {
        if (rebindingText != null) rebindingText.text = message;
    }
    private void RefreshSettingsLabels() {
        var texts = new [] { forwardBindingText, backwardBindingText, leftBindingText, rightBindingText,
            runBindingText, fireBindingText, reloadBindingText, pauseBindingText };
        for (int i = 0; i < texts.Length; i++)
            if (texts[i] != null) texts[i].text = BindingLabels[i];
        if (bindingButtonTexts != null)
            for (int i = 0; i < Mathf.Min(bindingButtonTexts.Length, bindings.Length); i++)
                if (bindingButtonTexts[i] != null) bindingButtonTexts[i].text = bindings[i].ToString();
        if (fullscreenIndicator != null) fullscreenIndicator.SetActive(fullscreenMode);
        if (windowedIndicator != null) windowedIndicator.SetActive(!fullscreenMode);
        if (mouseSensitivityXSlider != null) mouseSensitivityXSlider.SetValueWithoutNotify(MouseSensitivityX);
        if (mouseSensitivityYSlider != null) mouseSensitivityYSlider.SetValueWithoutNotify(MouseSensitivityY);
        if (mouseSensitivityXText != null) mouseSensitivityXText.text = $"X  {MouseSensitivityX:0.00}x";
        if (mouseSensitivityYText != null) mouseSensitivityYText.text = $"Y  {MouseSensitivityY:0.00}x";
    }
}