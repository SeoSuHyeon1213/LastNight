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
    [SerializeField] private Transform weaponAnchor;
    [SerializeField, Min(0)] private int gripIkLayer = 1;
    private WeaponGripProfile grip;
    private Transform leftElbowBone;
    private Transform rightElbowBone;
    private float leftBlend;
    private float rightBlend;
    private float fingerBlend;
    private int gripFrame = -1;
    private AnimatorCullingMode originalCulling;
    private Transform[] fingers = System.Array.Empty<Transform>();
    private Quaternion[] fingerBase = System.Array.Empty<Quaternion>();
    private WeaponGripProfile.FingerPose[] fingerPoses = System.Array.Empty<WeaponGripProfile.FingerPose>();
    private bool fingersApplied;
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
        if (!playerAnimator.isHuman) {
            Debug.LogError("PlayerShooter: Humanoid Animator가 필요합니다.", this);
            enabled = false;
            return;
        }
        leftElbowBone = playerAnimator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        rightElbowBone = playerAnimator.GetBoneTransform(HumanBodyBones.RightLowerArm);
        originalCulling = playerAnimator.cullingMode;
        playerAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
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
        RestoreFingers();
        if (gun != null) gun.gameObject.SetActive(false);
        activeSlot = index;
        gun.gameObject.SetActive(isActiveAndEnabled);
        grip = gun.GetComponent<WeaponGripProfile>();
        leftHandMount = grip != null && grip.leftHand != null ? grip.leftHand : FindHandle("Left Handle");
        rightHandMount = grip != null && grip.rightHand != null ? grip.rightHand : FindHandle("Right Handle");
        leftBlend = rightBlend = fingerBlend = 0f;
        gripFrame = -1;
        fingerPoses = grip != null && grip.fingerPose != null ? grip.fingerPose : System.Array.Empty<WeaponGripProfile.FingerPose>();
        fingers = new Transform[fingerPoses.Length];
        fingerBase = new Quaternion[fingerPoses.Length];
        for (int i = 0; i < fingers.Length; i++)
            if (WeaponGripProfile.IsFinger(fingerPoses[i].bone)) fingers[i] = playerAnimator.GetBoneTransform(fingerPoses[i].bone);
        if (aim != null) {
            if (!aimInitialized) aimInitialized = aim.Initialize(gun, gunPivot);
            else aim.SetWeapon(gun);
            gun.SetAimController(aim);
        }
    }

    private Transform FindHandle(string mountName) => WeaponGrip.FindMount(gun, mountName);

    private void OnEnable() {
        if (gun != null) gun.gameObject.SetActive(true);
        if (playerAnimator != null && playerAnimator.isHuman) playerAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
    }
    private void OnDisable() {
        RestoreFingers();
        leftBlend = rightBlend = fingerBlend = 0f;
        gripFrame = -1;
        if (playerAnimator != null) playerAnimator.cullingMode = originalCulling;
        foreach (Gun weapon in weapons)
            if (weapon != null) weapon.gameObject.SetActive(false);
    }

    private void Update() {
        RestoreFingers();
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
        if (!isActiveAndEnabled || gun == null || playerAnimator == null || layerIndex != gripIkLayer) return;
        if (weaponAnchor != null && !weaponAnchor.IsChildOf(gunPivot)) gunPivot.position = weaponAnchor.position;
        else if (rightElbowBone != null) gunPivot.position = rightElbowBone.position;
        if (aim != null) aim.ApplyWeaponAim();
        bool reloading = gun.state == Gun.State.Reloading;
        float step = Time.deltaTime / (grip != null ? Mathf.Max(0.01f, grip.blendSeconds) : 0.2f);
        leftBlend = Mathf.MoveTowards(leftBlend, reloading ? (grip != null ? grip.reloadLeftWeight : 0f) : 1f, step);
        rightBlend = Mathf.MoveTowards(rightBlend, reloading ? (grip != null ? grip.reloadRightWeight : 1f) : 1f, step);
        fingerBlend = Mathf.MoveTowards(fingerBlend, reloading ? 0f : 1f, step);
        Vector3 leftHint = leftElbowBone != null ? leftElbowBone.position : transform.position;
        Vector3 rightHint = rightElbowBone != null ? rightElbowBone.position : transform.position;
        if (grip != null) {
            leftHint += transform.TransformDirection(grip.leftElbowOffset);
            rightHint += transform.TransformDirection(grip.rightElbowOffset);
        }
        WeaponGrip.ApplyHands(playerAnimator, leftHandMount, rightHandMount, grip, leftBlend, rightBlend, leftHint, rightHint);
        gripFrame = Time.frameCount;
    }

    private void RestoreFingers() {
        if (!fingersApplied) return;
        for (int i = 0; i < fingers.Length; i++) if (fingers[i] != null) fingers[i].localRotation = fingerBase[i];
        fingersApplied = false;
    }

    private void ApplyFingers() {
        if (grip == null || gripFrame != Time.frameCount) return;
        for (int i = 0; i < fingers.Length; i++) {
            if (fingers[i] == null) continue;
            fingerBase[i] = fingers[i].localRotation;
            bool left = fingerPoses[i].bone <= HumanBodyBones.LeftLittleDistal;
            // 재장전 때 손가락 포즈도 놓아 탄창 조작 애니메이션이 보이게 한다.
            float weight = fingerBlend * (left ? leftBlend : rightBlend) * grip.fingerWeight;
            fingers[i].localRotation = Quaternion.Slerp(fingerBase[i], Quaternion.Euler(fingerPoses[i].localEulerAngles), weight);
        }
        fingersApplied = true;
    }

    private void LateUpdate() {
        GameSessionManager session = GameSessionManager.instance;
        if (gun == null || playerInput == null || Time.timeScale == 0f ||
            (session != null && (session.IsPaused || session.IsRebinding)) ||
            (GameManager.instance != null && GameManager.instance.isGameover)) return;
        // IK가 실행된 프레임에는 총을 다시 회전시키지 않는다.
        if (gripFrame != Time.frameCount && aim != null) aim.ApplyWeaponAim();
        ApplyFingers();
        if (Time.timeScale > 0f && playerInput.fire && !playerInput.reload) gun.Fire();
    }
}
