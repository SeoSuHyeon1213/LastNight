using UnityEngine;

// 플레이어 캐릭터를 조작하기 위한 사용자 입력을 감지
// 감지된 입력값을 다른 컴포넌트들이 사용할 수 있도록 제공
public class PlayerInput : MonoBehaviour, Cinemachine.AxisState.IInputAxisProvider {
    [SerializeField] private string lookXAxisName = "Mouse X";
    [SerializeField] private string lookYAxisName = "Mouse Y";

    // 카메라가 축을 갱신할 때 읽어 Update 실행 순서에 따른 입력 지연을 피한다.
    public float GetAxisValue(int axis) {
        GameSessionManager session = GameSessionManager.instance;
        if (!isActiveAndEnabled || !Application.isFocused || Time.timeScale == 0f
            || (GameManager.instance != null && GameManager.instance.isGameover)
            || (session != null && (session.IsPaused || session.IsRebinding)))
            return 0f;

        if (axis == 0)
            return Input.GetAxisRaw(lookXAxisName);
        if (axis == 1)
            return Input.GetAxisRaw(lookYAxisName);
        return 0f;
    }
    public string moveAxisName = "Vertical"; // 앞뒤 움직임을 위한 입력축 이름
    public string rotateAxisName = "Horizontal"; // 좌우 회전을 위한 입력축 이름
    public string fireButtonName = "Fire1"; // 발사를 위한 입력 버튼 이름
    public string reloadButtonName = "Reload"; // 재장전을 위한 입력 버튼 이름

    // 값 할당은 내부에서만 가능
    public float move { get; private set; } // 감지된 움직임 입력값
    public float rotate { get; private set; } // 감지된 회전 입력값
    public bool fire { get; private set; } // 감지된 발사 입력값
    public bool reload { get; private set; } // 감지된 재장전 입력값
    public bool IsRunning { get; private set; }
    public bool IsAiming { get; private set; }

    // 매프레임 사용자 입력을 감지
    private void Update() {
        GameSessionManager sessionManager = GameSessionManager.instance;

        // 게임오버 상태에서는 사용자 입력을 감지하지 않는다
        if ((GameManager.instance != null && GameManager.instance.isGameover)
            || (sessionManager != null && (sessionManager.IsPaused || sessionManager.IsRebinding)))
        {
            move = 0;
            rotate = 0;
            fire = false;
            reload = false;
            IsRunning = false;
            IsAiming = false;
            return;
        }

        if (sessionManager != null) {
            move = sessionManager.MoveInput;
            rotate = sessionManager.StrafeInput;
            fire = sessionManager.FireHeld;
            reload = sessionManager.ReloadPressed;
            IsRunning = sessionManager.IsRunning;
            IsAiming = Input.GetMouseButton(1);
            return;
        }

        move = Input.GetAxis(moveAxisName);
        rotate = Input.GetAxis(rotateAxisName);
        fire = Input.GetButton(fireButtonName);
        reload = Input.GetButtonDown(reloadButtonName);
        IsRunning = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        IsAiming = Input.GetMouseButton(1);
    }
}
