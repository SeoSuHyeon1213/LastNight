using UnityEngine;
using UnityEngine.EventSystems;

// 모바일 조작: 조이스틱 이동, HUD 버튼, 화면 오른쪽 드래그 시점 회전.
public sealed class MobileInputSource : PlayerInputSource {
    [SerializeField] private Joystick moveJoystick;
    [Tooltip("조이스틱을 이 비율 이상 밀면 달린다. 1보다 크면 끈다.")]
    [SerializeField, Range(0f, 1.01f)] private float autoRunThreshold = 1.01f;

    [Header("Buttons")]
    [SerializeField] private MobileInputButton fireButton;
    [SerializeField] private MobileInputButton reloadButton;
    [SerializeField] private MobileInputButton aimButton;
    [SerializeField] private MobileInputButton runButton;
    [SerializeField] private MobileInputButton grenadeButton;
    [SerializeField] private MobileInputButton interactButton;
    [SerializeField] private MobileInputButton weaponSwapButton;
    [Tooltip("지정하면 바리케이드를 설치할 수 있을 때만 상호작용 버튼을 표시한다.")]
    [SerializeField] private BarricadeBuilder barricadeBuilder;
    [Tooltip("켜면 조준 버튼을 누를 때마다 조준을 켜고 끈다. 끄면 누르는 동안만 조준한다.")]
    [SerializeField] private bool toggleAim = true;
    [Tooltip("켜면 달리기 버튼을 누를 때마다 달리기를 켜고 끈다. 끄면 누르는 동안만 달린다.")]
    [SerializeField] private bool toggleRun = true;
    [SerializeField] private string interactLabel = "Build";
    [Tooltip("HUD 상호작용 안내 문구. {0}: 상호작용 버튼 이름, {1}: 행동 이름")]
    [SerializeField] private string interactPromptFormat = "Tap {0} : {1}";

    [Header("Look")]
    [Tooltip("이 화면 가로 비율보다 오른쪽에서 시작한 터치만 시점 회전에 사용한다.")]
    [SerializeField, Range(0f, 1f)] private float lookAreaMinX = 0.4f;
    [Tooltip("터치 이동 픽셀을 카메라 입력으로 바꾸는 배율. 화면 높이 1080 기준이며 해상도에 맞춰 보정한다.")]
    [SerializeField, Min(0f)] private float lookScale = 0.1f;

    private bool aimToggled;
    private bool runToggled;
    private int lookFingerId = -1;
    private Vector2 lookDelta;

    public override string InteractLabel => interactLabel;

    public override string FormatInteractPrompt(string action) {
        try {
            return string.Format(interactPromptFormat, interactLabel, action);
        } catch (System.FormatException) {
            // Inspector에서 잘못 입력한 형식 때문에 HUD 갱신이 멈추지 않게 기본 문구로 대신한다.
            return base.FormatInteractPrompt(action);
        }
    }

    public override PlayerInputFrame ReadFrame() {
        var frame = new PlayerInputFrame();
        if (moveJoystick != null) frame.move = Vector2.ClampMagnitude(moveJoystick.Direction, 1f);
        frame.fire = fireButton != null && fireButton.IsHeld;
        frame.reload = reloadButton != null && reloadButton.ConsumePress();
        frame.grenade = grenadeButton != null && grenadeButton.ConsumePress();
        frame.interact = interactButton != null && interactButton.ConsumePress();
        frame.weaponSlot = weaponSwapButton != null && weaponSwapButton.ConsumePress() ? -1 : 0;

        if (aimButton != null && aimButton.ConsumePress() && toggleAim) aimToggled = !aimToggled;
        frame.aim = toggleAim ? aimToggled : aimButton != null && aimButton.IsHeld;
        if (runButton != null && runButton.ConsumePress() && toggleRun) runToggled = !runToggled;
        // 멈추면 달리기 토글을 해제해 다음 이동이 걷기로 시작하게 한다.
        if (frame.move.sqrMagnitude < 0.0001f) runToggled = false;
        frame.run = (toggleRun ? runToggled : runButton != null && runButton.IsHeld) ||
            frame.move.magnitude >= autoRunThreshold;
        return frame;
    }

    public override float GetLook(int axis) {
        if (axis == 0) return lookDelta.x;
        if (axis == 1) return lookDelta.y;
        return 0f;
    }

    private void Update() {
        UpdateInteractButton();
        lookDelta = Vector2.zero;
        float scale = lookScale * 1080f / Mathf.Max(1, Screen.height);
        for (int i = 0; i < Input.touchCount; i++) {
            Touch touch = Input.GetTouch(i);
            if (lookFingerId < 0) {
                // UI(조이스틱·버튼) 위에서 시작한 터치는 시점 회전에 쓰지 않는다.
                if (touch.phase != TouchPhase.Began || touch.position.x < Screen.width * lookAreaMinX ||
                    (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(touch.fingerId)))
                    continue;
                lookFingerId = touch.fingerId;
                continue;
            }
            if (touch.fingerId != lookFingerId) continue;
            if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled) {
                lookFingerId = -1;
                continue;
            }
            lookDelta += touch.deltaPosition * scale;
        }
    }

    private void UpdateInteractButton() {
        if (interactButton == null || barricadeBuilder == null) return;
        bool show = barricadeBuilder.CanInstallNow;
        if (interactButton.gameObject.activeSelf != show) interactButton.gameObject.SetActive(show);
    }

    private void OnDisable() {
        aimToggled = runToggled = false;
        lookFingerId = -1;
        lookDelta = Vector2.zero;
    }
}
