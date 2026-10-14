using UnityEngine;

// PC 조작: GameSessionManager의 키 바인딩(없으면 Input Manager 축)과 마우스를 읽는다.
public sealed class KeyboardMouseInputSource : PlayerInputSource {
    [SerializeField] private string lookXAxisName = "Mouse X";
    [SerializeField] private string lookYAxisName = "Mouse Y";
    [Header("GameSessionManager가 없을 때 사용")]
    [SerializeField] private string moveAxisName = "Vertical";
    [SerializeField] private string strafeAxisName = "Horizontal";
    [SerializeField] private string fireButtonName = "Fire1";
    [SerializeField] private string reloadButtonName = "Reload";
    [Header("Keys")]
    [SerializeField] private KeyCode grenadeKey = KeyCode.G;
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [SerializeField] private KeyCode primaryWeaponKey = KeyCode.Alpha1;
    [SerializeField] private KeyCode secondaryWeaponKey = KeyCode.Alpha2;

    public override string InteractLabel => interactKey.ToString();

    public override PlayerInputFrame ReadFrame() {
        var frame = new PlayerInputFrame();
        GameSessionManager session = GameSessionBase.instance as GameSessionManager;
        if (session != null) {
            frame.move = new Vector2(session.StrafeInput, session.MoveInput);
            frame.fire = session.FireHeld;
            frame.reload = session.ReloadPressed;
            frame.run = session.IsRunning;
        } else {
            frame.move = new Vector2(Input.GetAxis(strafeAxisName), Input.GetAxis(moveAxisName));
            frame.fire = Input.GetButton(fireButtonName);
            frame.reload = Input.GetButtonDown(reloadButtonName);
            frame.run = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        }
        frame.aim = Input.GetMouseButton(1);
        frame.grenade = Input.GetKeyDown(grenadeKey);
        frame.interact = Input.GetKeyDown(interactKey);
        if (Input.GetKeyDown(primaryWeaponKey)) frame.weaponSlot = 1;
        else if (Input.GetKeyDown(secondaryWeaponKey)) frame.weaponSlot = 2;
        else if (Input.mouseScrollDelta.y != 0f) frame.weaponSlot = -1;
        return frame;
    }

    public override float GetLook(int axis) {
        if (axis == 0) return Input.GetAxisRaw(lookXAxisName);
        if (axis == 1) return Input.GetAxisRaw(lookYAxisName);
        return 0f;
    }
}
