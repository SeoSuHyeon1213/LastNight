using UnityEngine;
using UnityEngine.SceneManagement;

// MobileIntro 메뉴용. 키 바인딩·커서·창 모드 없이 게임 시작, 시점 감도 설정, 종료 확인만 다룬다.
// 안드로이드 뒤로가기: 종료 확인창 → 닫기, 설정 창 → 닫기, 메인 메뉴 → 종료 확인창 열기.
public class MobileSceneChange : MonoBehaviour {
    // GameSessionBase와 같은 키를 써서 인트로에서 바꾼 감도가 게임 씬에 적용된다.
    private const string SensitivityXKey = "MouseSensitivity.X";
    private const string SensitivityYKey = "MouseSensitivity.Y";
    private const float MinSensitivity = 0.1f;
    private const float MaxSensitivity = 3f;

    [Header("Scenes")]
    [Tooltip("시작 버튼으로 불러올 씬 이름. Build Profile의 씬 목록에 포함해야 한다.")]
    [SerializeField] private string gameSceneName = "MobileGame";

    [Header("Intro Windows")]
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject quitConfirmationPanel;
    [Tooltip("선택 사항. Settings와 Quit 창을 포함하지 않는 메인 버튼 그룹을 연결하세요.")]
    [SerializeField] private GameObject mainMenuPanel;

    [Header("Look Sensitivity (선택 사항)")]
    [SerializeField] private UnityEngine.UI.Slider sensitivityXSlider;
    [SerializeField] private UnityEngine.UI.Slider sensitivityYSlider;
    [SerializeField] private UnityEngine.UI.Text sensitivityXText;
    [SerializeField] private UnityEngine.UI.Text sensitivityYText;

    private bool settingsDirty;
    private bool loadingScene;
    public float SensitivityX { get; private set; } = 1f;
    public float SensitivityY { get; private set; } = 1f;

    private void Awake() {
        LoadSettings();
        ConfigureSlider(sensitivityXSlider, SensitivityX);
        ConfigureSlider(sensitivityYSlider, SensitivityY);
        if (sensitivityXSlider != null) sensitivityXSlider.onValueChanged.AddListener(SetSensitivityX);
        if (sensitivityYSlider != null) sensitivityYSlider.onValueChanged.AddListener(SetSensitivityY);
        if (settingsPanel != null && !RequirePanel(settingsPanel, "Settings Panel")) return;
        if (quitConfirmationPanel != null && !RequirePanel(quitConfirmationPanel, "Quit Confirmation Panel")) return;
        ShowMainMenu();
    }

    private void Update() {
        // 레거시 Input에서 안드로이드 뒤로가기는 Escape로 들어온다.
        if (loadingScene || !Input.GetKeyDown(KeyCode.Escape)) return;
        if (quitConfirmationPanel != null && quitConfirmationPanel.activeInHierarchy) CancelQuit();
        else if (settingsPanel != null && settingsPanel.activeInHierarchy) CloseSettings();
        else if (quitConfirmationPanel != null) OpenQuitConfirmation();
    }

    private void OnDisable() { SaveSettings(); }

    private void OnDestroy() {
        if (sensitivityXSlider != null) sensitivityXSlider.onValueChanged.RemoveListener(SetSensitivityX);
        if (sensitivityYSlider != null) sensitivityYSlider.onValueChanged.RemoveListener(SetSensitivityY);
    }

    // 모바일에서는 앱 전환 후 종료될 수 있으므로 백그라운드로 갈 때 저장한다.
    private void OnApplicationPause(bool pauseStatus) {
        if (pauseStatus) SaveSettings();
    }

    private void OnApplicationQuit() { SaveSettings(); }

    public void StartGame() {
        if (loadingScene) return;
        if (string.IsNullOrEmpty(gameSceneName) || !Application.CanStreamedLevelBeLoaded(gameSceneName)) {
            Debug.LogError($"MobileSceneChange: '{gameSceneName}' 씬을 Build Profile 씬 목록에 추가하세요.", this);
            return;
        }
        SaveSettings();
        Time.timeScale = 1f;
        loadingScene = true;
        SceneManager.LoadScene(gameSceneName);
    }

    public void OpenSettings() {
        if (!RequirePanel(settingsPanel, "Settings Panel")) return;
        LoadSettings();
        if (quitConfirmationPanel != null) quitConfirmationPanel.SetActive(false);
        SetMainMenuVisible(false);
        settingsPanel.SetActive(true);
        RefreshSettingsLabels();
    }

    public void CloseSettings() { ShowMainMenu(); }

    public void OpenQuitConfirmation() {
        if (!RequirePanel(quitConfirmationPanel, "Quit Confirmation Panel")) return;
        SaveSettings();
        if (settingsPanel != null) settingsPanel.SetActive(false);
        SetMainMenuVisible(false);
        quitConfirmationPanel.SetActive(true);
    }

    public void CancelQuit() { ShowMainMenu(); }

    public void QuitGame() {
        // 메인 메뉴의 나가기 버튼은 OpenQuitConfirmation에 연결한다.
        if (quitConfirmationPanel == null || !quitConfirmationPanel.activeInHierarchy) {
            Debug.LogWarning("MobileSceneChange: 종료 확인창을 연 뒤 확인 버튼으로 종료하세요.", this);
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
        SaveSettings();
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (quitConfirmationPanel != null) quitConfirmationPanel.SetActive(false);
        SetMainMenuVisible(true);
    }

    private void SetMainMenuVisible(bool visible) {
        if (mainMenuPanel == null) return;
        if (!visible && (mainMenuPanel == gameObject || transform.IsChildOf(mainMenuPanel.transform)
            || (settingsPanel != null && settingsPanel.transform.IsChildOf(mainMenuPanel.transform))
            || (quitConfirmationPanel != null && quitConfirmationPanel.transform.IsChildOf(mainMenuPanel.transform)))) {
            Debug.LogError("MobileSceneChange: Main Menu Panel은 컨트롤러와 설정/종료 창을 포함하지 않는 버튼 그룹이어야 합니다.", this);
            return;
        }
        mainMenuPanel.SetActive(visible);
    }

    private bool RequirePanel(GameObject panel, string field) {
        if (panel != null && panel != gameObject && !transform.IsChildOf(panel.transform)) return true;
        Debug.LogError($"MobileSceneChange: {field}을 연결하고 MobileSceneChange를 창 밖의 항상 활성인 오브젝트에 배치하세요.", this);
        return false;
    }

    private void LoadSettings() {
        SensitivityX = ClampSensitivity(PlayerPrefs.GetFloat(SensitivityXKey, 1f));
        SensitivityY = ClampSensitivity(PlayerPrefs.GetFloat(SensitivityYKey, 1f));
    }

    private void SaveSettings() {
        if (!settingsDirty) return;
        PlayerPrefs.Save();
        settingsDirty = false;
    }

    private static float ClampSensitivity(float value) {
        return float.IsNaN(value) || float.IsInfinity(value) ? 1f : Mathf.Clamp(value, MinSensitivity, MaxSensitivity);
    }

    private static void ConfigureSlider(UnityEngine.UI.Slider slider, float value) {
        if (slider == null) return;
        slider.minValue = MinSensitivity;
        slider.maxValue = MaxSensitivity;
        slider.wholeNumbers = false;
        slider.SetValueWithoutNotify(value);
    }

    public void SetSensitivityX(float value) {
        SensitivityX = ClampSensitivity(value);
        PlayerPrefs.SetFloat(SensitivityXKey, SensitivityX);
        settingsDirty = true;
        RefreshSettingsLabels();
    }

    public void SetSensitivityY(float value) {
        SensitivityY = ClampSensitivity(value);
        PlayerPrefs.SetFloat(SensitivityYKey, SensitivityY);
        settingsDirty = true;
        RefreshSettingsLabels();
    }

    private void RefreshSettingsLabels() {
        if (sensitivityXSlider != null) sensitivityXSlider.SetValueWithoutNotify(SensitivityX);
        if (sensitivityYSlider != null) sensitivityYSlider.SetValueWithoutNotify(SensitivityY);
        if (sensitivityXText != null) sensitivityXText.text = $"X  {SensitivityX:0.00}x";
        if (sensitivityYText != null) sensitivityYText.text = $"Y  {SensitivityY:0.00}x";
    }
}
