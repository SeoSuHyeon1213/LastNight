using UnityEngine;
using UnityEngine.Serialization;

public class PlayerShooter : MonoBehaviour {
    public Gun primaryGun;
    public Gun secondaryGun;
    public Transform gunPivot;
    [FormerlySerializedAs("gun"), SerializeField] private Gun legacyGun;
    public Transform leftHandMount;
    public Transform rightHandMount;

    private readonly Gun[] weapons = new Gun[2];
    private readonly Gun[] sources = new Gun[2];
    private PlayerInput playerInput;
    private Animator playerAnimator;
    private PlayerAimController aim;
    private bool aimInitialized;
    private int activeSlot;
    public Gun gun => weapons[activeSlot];
    public Gun PrimaryGun => weapons[0];
    public Gun SecondaryGun => weapons[1];
    public int ActiveSlot => activeSlot + 1;

    private void Awake() {
        playerInput = GetComponent<PlayerInput>();
        playerAnimator = GetComponent<Animator>();
        aim = GetComponent<PlayerAimController>();
        if (gunPivot == null || playerInput == null || playerAnimator == null) {
            Debug.LogError("PlayerShooter: Gun Pivot, PlayerInput, Animator가 필요합니다.", this);
            enabled = false;
            return;
        }
        if (primaryGun != null) {
            foreach (Gun old in gunPivot.GetComponentsInChildren<Gun>(true))
                old.gameObject.SetActive(false);
            EquipWeapon(1, primaryGun);
        } else if (legacyGun != null) {
            weapons[0] = legacyGun;
            SelectWeapon(0);
        } else {
            Debug.LogError("PlayerShooter: Primary Gun 프리팹을 지정하세요.", this);
        }
        if (secondaryGun != null) EquipWeapon(2, secondaryGun);
    }

    public bool TryAcquireWeapon(int slot, Gun prefab, int duplicateAmmo) {
        if (slot < 1 || slot > 2 || prefab == null || gunPivot == null) return false;
        int index = slot - 1;
        if (sources[index] == prefab && weapons[index] != null) {
            weapons[index].AddAmmo(duplicateAmmo);
            return true;
        }
        return EquipWeapon(slot, prefab);
    }

    public bool EquipWeapon(int slot, Gun prefab) {
        if (slot < 1 || slot > 2 || prefab == null || gunPivot == null) return false;
        if (prefab.gunData == null || prefab.fireTransform == null) {
            Debug.LogError("PlayerShooter: 무기 프리팹의 GunData와 Fire Transform을 확인하세요.", prefab);
            return false;
        }
        int index = slot - 1;
        Gun replacement = Instantiate(prefab, gunPivot, false);
        replacement.gameObject.SetActive(false);
        if (weapons[index] != null) {
            weapons[index].gameObject.SetActive(false);
            Destroy(weapons[index].gameObject);
        }
        weapons[index] = replacement;
        sources[index] = prefab;
        if (index == activeSlot) SelectWeapon(index);
        return true;
    }

    private void SelectWeapon(int index) {
        if (weapons[index] == null) return;
        if (gun != null) gun.gameObject.SetActive(false);
        activeSlot = index;
        gun.gameObject.SetActive(isActiveAndEnabled);
        leftHandMount = FindHandle("Left Handle");
        rightHandMount = FindHandle("Right Handle");
        if (aim != null) {
            if (!aimInitialized) aimInitialized = aim.Initialize(gun, gunPivot);
            else aim.SetWeapon(gun);
            gun.SetAimController(aim);
        }
    }

    private Transform FindHandle(string mountName) {
        foreach (Transform child in gun.GetComponentsInChildren<Transform>(true)) {
            string normalized = child.name.Replace(" ", "").Replace("Handel", "Handle");
            if (string.Equals(normalized, mountName.Replace(" ", ""), System.StringComparison.OrdinalIgnoreCase))
                return child;
        }
        return WeaponGrip.FindMount(gun, mountName);
    }

    private void OnEnable() { if (gun != null) gun.gameObject.SetActive(true); }
    private void OnDisable() {
        foreach (Gun weapon in weapons)
            if (weapon != null) weapon.gameObject.SetActive(false);
    }

    private void Update() {
        GameSessionManager session = GameSessionManager.instance;
        if (Time.timeScale == 0f || (session != null && (session.IsPaused || session.IsRebinding))) return;
        if (Input.GetKeyDown(KeyCode.Alpha1)) SelectWeapon(0);
        else if (Input.GetKeyDown(KeyCode.Alpha2)) SelectWeapon(1);
        else if (Input.mouseScrollDelta.y != 0f) SelectWeapon(1 - activeSlot);
        if (gun != null && playerInput.reload && gun.Reload()) playerAnimator.SetTrigger("Reload");
        UIManager ui = UIManager.instance;
        if (gun != null && ui != null && ui.ammoText != null)
            ui.UpdateAmmoText(gun.magAmmo, gun.ammoRemain, gun.HasInfiniteAmmo);
    }

    private void OnAnimatorIK(int layerIndex) {
        if (gun == null || playerAnimator == null) return;
        gunPivot.position = playerAnimator.GetIKHintPosition(AvatarIKHint.RightElbow);
        if (aim != null) aim.ApplyWeaponAim();
        WeaponGrip.ApplyHands(playerAnimator, leftHandMount, rightHandMount);
    }

    private void LateUpdate() {
        GameSessionManager session = GameSessionManager.instance;
        if (gun == null || playerInput == null || Time.timeScale == 0f ||
            (session != null && (session.IsPaused || session.IsRebinding)) ||
            (GameManager.instance != null && GameManager.instance.isGameover)) return;
        // Shooting must also work when an Animator layer does not invoke IK.
        // Keep muzzle obstruction and same-frame aim validation before firing.
        if (aim != null) aim.ApplyWeaponAim();
        if (Time.timeScale > 0f && playerInput.fire && !playerInput.reload) gun.Fire();
    }
}
