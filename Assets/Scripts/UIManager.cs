using UnityEngine;
using UnityEngine.SceneManagement; // 씬 관리자 관련 코드
using UnityEngine.UI; // UI 관련 코드

// 필요한 UI에 즉시 접근하고 변경할 수 있도록 허용하는 UI 매니저

public class UIManager : MonoBehaviour {
    // 싱글톤 접근용 프로퍼티
    public static UIManager instance //UImanager 타입의 intance를 반환하는 프로퍼티
    {
        get
        {
            if (m_instance == null)
            {
                m_instance = FindAnyObjectByType<UIManager>();
            }

            return m_instance;
        }
    }
    // 싱글톤 접근용 프로퍼티를 통해 UIManager에 접근시
    // UIManager uimanager = UIManager.instance; 와 같이 접근 가능
    // UIManager.instance.ammoText.text = "10/100"; // 탄약 표시
    // instance를 통해 접근하는 이유는 다른 외부 클래스들이 UIManager를 직접 생성하지 않고, 이미 존재하는 UIManager를 공유해서 사용하기 위함
    private static UIManager m_instance; // 싱글톤이 할당될 변수

    public Text ammoText; // 탄약 표시용 텍스트
    public Text scoreText; // 점수 표시용 텍스트
    public Text waveText; // 적 웨이브 표시용 텍스트
    public GameObject gameoverUI; // 게임 오버시 활성화할 UI 

    private void Awake() {
        if (m_instance == null)
            m_instance = this;

    }

    // 탄약 텍스트 갱신
    public void UpdateAmmoText(int magAmmo, int remainAmmo) {
        ammoText.text = magAmmo + "/" + remainAmmo;
    }

    // 점수 텍스트 갱신
    public void UpdateScoreText(int newScore) {
        scoreText.text = "Score : " + newScore;
    }

    // 적 웨이브 텍스트 갱신
    public void UpdateWaveText(int waves, int count) {
        waveText.text = "Wave : " + waves + "\nEnemy Left : " + count;
    }

    // 게임 오버 UI 활성화
    public void SetActiveGameoverUI(bool active) {
        gameoverUI.SetActive(active);
    }

    // 게임 재시작
    public void GameRestart() {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name); // 현재 씬을 다시 로드 
    }
}
