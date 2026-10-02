using UnityEngine;
using Cinemachine;

// UI 조준 기준을 월드 목표로 바꾸고, 손 IK 전에 무기 전체를 정렬한다.
[DisallowMultipleComponent]
[DefaultExecutionOrder(100)]
public class PlayerAimController : MonoBehaviour {
    [SerializeField] private Camera viewCamera;
    [SerializeField] private RectTransform aimDot;
    [SerializeField] private string aimDotName = "Aimdot";
    [SerializeField] private LayerMask collisionMask = Physics.DefaultRaycastLayers;
    [SerializeField, Range(1f, 89f)] private float maxPitch = 85f;
    [SerializeField, Range(1f, 179f)] private float maxYaw = 100f;
    [SerializeField, Min(0.001f)] private float muzzleClearance = 0.02f;
    [SerializeField, Min(0.01f)] private float alignmentTolerance = 1f;
    [Header("Aim Zoom")]
    [SerializeField] private CinemachineFreeLook aimCamera;
    [SerializeField, Range(1f, 179f)] private float aimFieldOfView = 30f;
    [SerializeField, Min(1f), Tooltip("초당 FOV 변화량")]
    private float fieldOfViewChangeSpeed = 120f;
    private PlayerInput playerInput;
    private bool fieldOfViewOverridden;
    private float originalFieldOfView;
    private readonly CinemachineVirtualCamera[] zoomRigs = new CinemachineVirtualCamera[3];
    private readonly float[] originalRigFieldOfView = new float[3];
    private Gun gun;
    private Transform pivot;
    private Canvas canvas;
    private Quaternion initialRotation;
    private bool initialized;
    private bool validAim;
    private int aimFrame = -1;
    private readonly RaycastHit[] hits = new RaycastHit[32];
    private readonly Collider[] overlaps = new Collider[32];
    public Vector3 AimTarget { get; private set; }

    private void Awake() {
        playerInput = GetComponent<PlayerInput>();
    }

    private void Start() {
        if (viewCamera == null) viewCamera = Camera.main;
        if (aimCamera == null) {
            PlayerMovement movement = GetComponent<PlayerMovement>();
            if (movement != null) aimCamera = movement.FreeLookCamera;
        }
        if (aimCamera == null && viewCamera != null && viewCamera.GetComponent<CinemachineBrain>() != null)
            Debug.LogWarning("PlayerAimController: Aim Camera에 플레이어의 Cinemachine FreeLook을 연결하세요.", this);
    }

    private void Update() {
        UpdateAimFieldOfView();
    }

    public void UpdateAimFieldOfView() {
        GameSessionManager session = GameSessionManager.instance;
        if (!isActiveAndEnabled || playerInput == null || !playerInput.isActiveAndEnabled ||
            !Application.isFocused || Time.timeScale == 0f ||
            (GameManager.instance != null && GameManager.instance.isGameover) ||
            (session != null && (session.IsPaused || session.IsRebinding))) {
            RestoreFieldOfView();
            return;
        }
        // Cinemachine가 있는 카메라는 실제 Camera 값을 매 프레임 덮어쓰므로 Lens를 조절한다.
        bool aiming = playerInput.IsAiming;
        if (!fieldOfViewOverridden) {
            if (!aiming || (aimCamera == null && viewCamera == null)) return;
            if (aimCamera != null) {
                originalFieldOfView = aimCamera.m_Lens.FieldOfView;
                for (int i = 0; i < zoomRigs.Length; i++) {
                    zoomRigs[i] = aimCamera.GetRig(i);
                    if (zoomRigs[i] != null) originalRigFieldOfView[i] = zoomRigs[i].m_Lens.FieldOfView;
                }
            } else originalFieldOfView = viewCamera.fieldOfView;
            fieldOfViewOverridden = true;
        }
        float step = fieldOfViewChangeSpeed * Time.deltaTime;
        float target = aiming ? aimFieldOfView : originalFieldOfView;
        bool restored;
        if (aimCamera != null) {
            aimCamera.m_Lens.FieldOfView = Mathf.MoveTowards(aimCamera.m_Lens.FieldOfView, target, step);
            restored = Mathf.Approximately(aimCamera.m_Lens.FieldOfView, originalFieldOfView);
            if (!aimCamera.m_CommonLens) {
                for (int i = 0; i < zoomRigs.Length; i++) {
                    if (zoomRigs[i] == null) continue;
                    float rigTarget = aiming ? aimFieldOfView : originalRigFieldOfView[i];
                    zoomRigs[i].m_Lens.FieldOfView = Mathf.MoveTowards(zoomRigs[i].m_Lens.FieldOfView, rigTarget, step);
                    restored &= Mathf.Approximately(zoomRigs[i].m_Lens.FieldOfView, originalRigFieldOfView[i]);
                }
            }
        } else if (viewCamera != null) {
            viewCamera.fieldOfView = Mathf.MoveTowards(viewCamera.fieldOfView, target, step);
            restored = Mathf.Approximately(viewCamera.fieldOfView, originalFieldOfView);
        } else {
            RestoreFieldOfView();
            return;
        }
        if (!aiming && restored) RestoreFieldOfView();
    }

    private void RestoreFieldOfView() {
        if (!fieldOfViewOverridden) return;
        if (aimCamera != null) {
            aimCamera.m_Lens.FieldOfView = originalFieldOfView;
            for (int i = 0; i < zoomRigs.Length; i++)
                if (zoomRigs[i] != null) zoomRigs[i].m_Lens.FieldOfView = originalRigFieldOfView[i];
        } else if (viewCamera != null) viewCamera.fieldOfView = originalFieldOfView;
        fieldOfViewOverridden = false;
    }

    public bool Initialize(Gun weapon, Transform weaponPivot) {
        if (initialized) return gun == weapon && pivot == weaponPivot;
        gun = weapon;
        pivot = weaponPivot;
        if (viewCamera == null) viewCamera = Camera.main;
        if (aimDot == null) {
            foreach (var candidate in FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None)) {
                if (candidate.name != aimDotName) continue;
                if (aimDot != null) return Fail("Aimdot이 여러 개입니다. Aim Dot을 직접 연결하세요.");
                aimDot = candidate;
            }
        }
        if (gun == null || pivot == null || gun.fireTransform == null || viewCamera == null || aimDot == null)
            return Fail("Gun, Gun Pivot, Fire Transform, View Camera, Aim Dot 참조를 확인하세요.");
        if (!gun.fireTransform.IsChildOf(pivot) || pivot == gun.fireTransform)
            return Fail("Fire Transform은 총 전체를 회전시키는 Gun Pivot의 자식이어야 합니다.");
        canvas = aimDot.GetComponentInParent<Canvas>();
        if (canvas == null || (canvas.renderMode != RenderMode.ScreenSpaceOverlay && canvas.worldCamera == null))
            return Fail("Aimdot의 Canvas와 Canvas World Camera를 확인하세요.");
        if (collisionMask.value == 0) return Fail("Collision Mask에 지면, 벽, 적 레이어를 포함하세요.");
        initialRotation = pivot.localRotation;
        initialized = true;
        return true;
    }

    public bool SetWeapon(Gun weapon) {
        if (!initialized || weapon == null || weapon.fireTransform == null || pivot == null ||
            !weapon.fireTransform.IsChildOf(pivot)) return false;
        gun = weapon;
        validAim = false;
        aimFrame = -1;
        return true;
    }

    private bool Fail(string reason) {
        Debug.LogError("PlayerAimController: " + reason, this);
        enabled = false;
        return false;
    }

    private bool IsOwner(Collider other) {
        return other.transform.IsChildOf(transform) || other.transform.IsChildOf(gun.transform);
    }

    private bool Cast(Vector3 origin, Vector3 direction, float distance, out RaycastHit closest) {
        int count = Physics.RaycastNonAlloc(origin, direction, hits, distance, collisionMask, QueryTriggerInteraction.Ignore);
        RaycastHit[] results = count == hits.Length
            ? Physics.RaycastAll(origin, direction, distance, collisionMask, QueryTriggerInteraction.Ignore) : hits;
        if (results != hits) count = results.Length;
        closest = default;
        float nearest = float.PositiveInfinity;
        for (int i = 0; i < count; i++) {
            if (!IsOwner(results[i].collider) && results[i].distance < nearest) {
                closest = results[i];
                nearest = closest.distance;
            }
        }
        return nearest < float.PositiveInfinity;
    }

    public void ApplyWeaponAim() {
        validAim = false;
        if (!initialized || !isActiveAndEnabled || Time.timeScale == 0f || gun == null || pivot == null ||
            viewCamera == null || aimDot == null || canvas == null || gun.fireTransform == null) return;
        Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(uiCamera, aimDot.TransformPoint(aimDot.rect.center));
        Ray ray = viewCamera.ScreenPointToRay(screen);
        float range = gun.FireDistance + Vector3.Distance(ray.origin, gun.fireTransform.position);
        AimTarget = Cast(ray.origin, ray.direction, range, out var hit) ? hit.point : ray.GetPoint(range);
        pivot.localRotation = initialRotation;
        Vector3 targetVector = AimTarget - pivot.position;
        if (targetVector.sqrMagnitude < 0.0001f) return;
        Vector3 local = transform.InverseTransformDirection(targetVector.normalized);
        float yaw = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
        float pitch = Mathf.Atan2(-local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg;
        float clampedYaw = Mathf.Clamp(yaw, -maxYaw, maxYaw);
        float clampedPitch = Mathf.Clamp(pitch, -maxPitch, maxPitch);
        Vector3 limitedVector = transform.TransformDirection(Quaternion.Euler(clampedPitch, clampedYaw, 0f) * Vector3.forward) * targetVector.magnitude;
        Vector3 muzzleOffset = gun.fireTransform.position - pivot.position;
        Vector3 forward = gun.fireTransform.forward;
        // 회전하면 총구 위치도 이동한다. 피벗 중심 구와 총구 광선의 교점으로 근거리 오차를 보정한다.
        float projection = Vector3.Dot(muzzleOffset, forward);
        float discriminant = projection * projection + limitedVector.sqrMagnitude - muzzleOffset.sqrMagnitude;
        if (discriminant < 0f) return;
        float distance = -projection + Mathf.Sqrt(discriminant);
        if (distance <= muzzleClearance) return;
        Vector3 from = muzzleOffset + forward * distance;
        pivot.rotation = Quaternion.FromToRotation(from, limitedVector) * pivot.rotation;
        aimFrame = Time.frameCount;
        validAim = Vector3.Angle(gun.fireTransform.forward, AimTarget - gun.fireTransform.position) <= alignmentTolerance;
    }

    public bool TryGetShot(out RaycastHit hit, out bool hasHit, out Vector3 point) {
        hit = default;
        hasHit = false;
        point = default;
        if (!initialized || !isActiveAndEnabled || !validAim || aimFrame != Time.frameCount || gun == null ||
            !gun.isActiveAndEnabled || pivot == null || gun.fireTransform == null) return false;
        Vector3 muzzle = gun.fireTransform.position;
        int count = Physics.OverlapSphereNonAlloc(muzzle, muzzleClearance, overlaps, collisionMask, QueryTriggerInteraction.Ignore);
        if (count == overlaps.Length) return false;
        for (int i = 0; i < count; i++) if (!IsOwner(overlaps[i])) return false;
        Vector3 path = muzzle - pivot.position;
        if (path.sqrMagnitude > 0.0001f && Cast(pivot.position, path.normalized, path.magnitude, out _)) return false;
        hasHit = Cast(muzzle, gun.fireTransform.forward, gun.FireDistance, out hit);
        point = hasHit ? hit.point : muzzle + gun.fireTransform.forward * gun.FireDistance;
        return true;
    }

    // Gun의 발사 가능 여부 검사 후, 총구에서 개별 산탄 방향을 조회한다.
    public bool TryGetDirectionalShotHit(Vector3 direction, out RaycastHit hit) {
        hit = default;
        if (!initialized || gun == null || gun.fireTransform == null) return false;
        return Cast(gun.fireTransform.position, direction, gun.FireDistance, out hit);
    }

    // TryGetShot으로 발사 가능 여부를 확인한 뒤 같은 마스크·자기 제외 규칙으로 조회한다.
    public RaycastHit[] GetOrderedShotHits() {
        if (!initialized || gun == null || gun.fireTransform == null)
            return System.Array.Empty<RaycastHit>();
        RaycastHit[] results = Physics.RaycastAll(gun.fireTransform.position, gun.fireTransform.forward,
            gun.FireDistance, collisionMask, QueryTriggerInteraction.Ignore);
        results = System.Array.FindAll(results, h => !IsOwner(h.collider));
        System.Array.Sort(results, (a, b) => a.distance.CompareTo(b.distance));
        return results;
    }

    private void OnDisable() {
        RestoreFieldOfView();
        validAim = false;
        aimFrame = -1;
        if (initialized && pivot != null) pivot.localRotation = initialRotation;
    }

    private void OnValidate() {
        aimFieldOfView = Mathf.Clamp(aimFieldOfView, 1f, 179f);
        fieldOfViewChangeSpeed = Mathf.Max(1f, fieldOfViewChangeSpeed);
        maxPitch = Mathf.Clamp(maxPitch, 1f, 89f);
        maxYaw = Mathf.Clamp(maxYaw, 1f, 179f);
        muzzleClearance = Mathf.Max(0.001f, muzzleClearance);
        alignmentTolerance = Mathf.Max(0.01f, alignmentTolerance);
    }
}
