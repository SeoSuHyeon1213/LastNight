using UnityEngine;

// 플레이어 캐릭터를 조작하기 위한 사용자 입력을 감지
// 감지된 입력값을 다른 컴포넌트들이 사용할 수 있도록 제공
// 실제 입력 장치는 씬별 PlayerInputSource(키보드·마우스, 모바일)가 담당한다.
[DefaultExecutionOrder(-50)]
public class PlayerInput : MonoBehaviour, Cinemachine.AxisState.IInputAxisProvider {
    [Tooltip("이 씬에서 사용할 입력 소스. 비워 두면 같은 오브젝트의 PlayerInputSource를 사용한다.")]
    [SerializeField] private PlayerInputSource source;

    public PlayerInputSource Source => source;

    private void Awake() {
        if (source == null) source = GetComponent<PlayerInputSource>();
        if (source == null) {
            Debug.LogError("PlayerInput: 사용할 PlayerInputSource(KeyboardMouseInputSource 또는 MobileInputSource)를 연결하세요.", this);
            enabled = false;
        }
    }

    private void Start() {
        PlayerMovement movement = GetComponent<PlayerMovement>();
        if (movement == null || movement.FreeLookCamera == null) return;
        // FreeLook과 플레이어가 별도 오브젝트여도 감도가 적용된 입력을 사용한다.
        movement.FreeLookCamera.m_XAxis.SetInputAxisProvider(0, this);
        movement.FreeLookCamera.m_YAxis.SetInputAxisProvider(1, this);
    }

    // 카메라가 축을 갱신할 때 읽어 Update 실행 순서에 따른 입력 지연을 피한다.
    public float GetAxisValue(int axis) {
        GameSessionManager session = GameSessionManager.instance;
        if (!isActiveAndEnabled || source == null || !Application.isFocused || Time.timeScale == 0f
            || (GameManager.instance != null && GameManager.instance.isGameover)
            || (session != null && (session.IsPaused || session.IsRebinding)))
            return 0f;

        if (axis == 0)
            return source.GetLook(0) * (session != null ? session.MouseSensitivityX : 1f);
        if (axis == 1)
            return source.GetLook(1) * (session != null ? session.MouseSensitivityY : 1f);
        return 0f;
    }

    // 값 할당은 내부에서만 가능
    public float move { get; private set; } // 감지된 움직임 입력값
    public float rotate { get; private set; } // 감지된 회전 입력값
    public bool fire { get; private set; } // 감지된 발사 입력값
    public bool reload { get; private set; } // 감지된 재장전 입력값
    public bool IsRunning { get; private set; }
    public bool IsAiming { get; private set; }
    public bool GrenadePressed { get; private set; }
    public bool InteractPressed { get; private set; }
    // 0: 요청 없음, 1·2: 해당 슬롯, -1: 다른 무기로 전환
    public int WeaponSlotRequest { get; private set; }
    public string InteractLabel => source != null ? source.InteractLabel : string.Empty;

    // 매프레임 사용자 입력을 감지. 다른 컴포넌트보다 먼저 한 번 읽어 같은 프레임 값을 공유한다.
    private void Update() {
        GameSessionManager sessionManager = GameSessionManager.instance;
        PlayerInputFrame frame = source != null ? source.ReadFrame() : default;

        // 게임오버 상태에서는 사용자 입력을 감지하지 않는다
        if ((GameManager.instance != null && GameManager.instance.isGameover)
            || (sessionManager != null && (sessionManager.IsPaused || sessionManager.IsRebinding)))
            frame = default;

        move = frame.move.y;
        rotate = frame.move.x;
        fire = frame.fire;
        reload = frame.reload;
        IsRunning = frame.run;
        IsAiming = frame.aim;
        GrenadePressed = frame.grenade;
        InteractPressed = frame.interact;
        WeaponSlotRequest = frame.weaponSlot;
    }
}
