using UnityEngine;
using UnityEngine.SceneManagement;

// 모바일 게임 씬 세션: HUD 일시정지 버튼, 안드로이드 뒤로가기, 앱 전환 시 자동 일시정지.
// 키 바인딩·커서·창 모드는 없다. 일시정지 버튼은 PauseGame에 연결한다.
public sealed class MobileGameSessionManager : GameSessionBase {
    [Tooltip("켜면 안드로이드 뒤로가기로 일시정지 창을 열고 닫는다. 설정 창에서는 일시정지 창으로 돌아간다.")]
    [SerializeField] private bool backButtonTogglesPause = true;
    [Tooltip("켜면 홈 버튼·전화 등으로 앱이 백그라운드로 갈 때 일시정지한다.")]
    [SerializeField] private bool pauseWhenBackgrounded = true;
    [Tooltip("메인 메뉴 버튼으로 이동할 씬 이름. Build Profile의 씬 목록에 포함해야 한다.")]
    [SerializeField] private string mainSceneName = "MobileIntro";

    protected override bool ReadPauseToggle() {
        // 레거시 Input에서 안드로이드 뒤로가기는 Escape로 들어온다.
        if (!backButtonTogglesPause || !Input.GetKeyDown(KeyCode.Escape)) return false;
        if (IsSettingsOpen) {
            CloseSettings();
            return false;
        }
        return true;
    }

    // 에디터에서는 창 포커스만 잃어도 호출되어 테스트 중 일시정지되므로 기기에서만 적용한다.
    private void OnApplicationPause(bool pauseStatus) {
        if (pauseStatus && pauseWhenBackgrounded && !Application.isEditor && instance == this && !IsPaused) PauseGame();
    }

    public override void LoadMainScene() {
        if (string.IsNullOrEmpty(mainSceneName) || !Application.CanStreamedLevelBeLoaded(mainSceneName)) {
            Debug.LogError($"MobileGameSessionManager: '{mainSceneName}' 씬을 Build Profile 씬 목록에 추가하세요.", this);
            return;
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene(mainSceneName);
    }
}
