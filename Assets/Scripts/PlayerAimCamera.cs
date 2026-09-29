using Cinemachine;
using UnityEngine;

// 입력과 탄착 계산은 기존 소유자에 두고 단일 FreeLook의 렌즈/위치만 전환한다.
[DisallowMultipleComponent]
[DefaultExecutionOrder(-100)]
public class PlayerAimCamera : MonoBehaviour {
    [SerializeField] private CinemachineFreeLook freeLookCamera;
    [SerializeField] private PlayerInput playerInput;
    [SerializeField] private PlayerShooter shooter;
    [Tooltip("켜면 시작 시 FreeLook의 FOV를 일반 모드 값으로 사용합니다.")]
    [SerializeField] private bool useInitialFieldOfView = true;
    [SerializeField, Range(1f, 179f)] private float normalFieldOfView = 60f;
    [SerializeField, Range(1f, 179f)] private float aimFieldOfView = 40f;
    [Tooltip("기존 카메라 위치 기준 오프셋. X: 오른쪽, Y: 위쪽, Z: 전방.")]
    [SerializeField] private Vector3 normalOffset = Vector3.zero;
    [SerializeField] private Vector3 aimOffset = new Vector3(0.35f, 0.1f, 0.8f);
    [SerializeField, Min(0.01f)] private float transitionDuration = 0.2f;
    [Tooltip("CinemachineCollider가 없는 경우 생성할 충돌 설정입니다. 플레이어 레이어는 제외하세요.")]
    [SerializeField] private LayerMask cameraCollisionMask = Physics.DefaultRaycastLayers;
    [SerializeField, Min(0.01f)] private float cameraRadius = 0.2f;
    private CinemachineCameraOffset offset;
    private float initialFov;
    private Vector3 initialOffset;
    private bool initialCommonLens;
    private bool ready;
    private float blend;

    private void Start() {
        if (playerInput == null) playerInput = GetComponent<PlayerInput>();
        if (shooter == null) shooter = GetComponent<PlayerShooter>();
        if (freeLookCamera == null) {
            PlayerMovement movement = GetComponent<PlayerMovement>();
            if (movement != null) freeLookCamera = movement.FreeLookCamera;
        }
        if (playerInput == null || shooter == null || freeLookCamera == null) {
            Debug.LogError("PlayerAimCamera: PlayerInput, PlayerShooter, FreeLook 참조를 확인하세요.", this);
            enabled = false;
            return;
        }
        initialFov = freeLookCamera.m_Lens.FieldOfView;
        initialCommonLens = freeLookCamera.m_CommonLens;
        if (useInitialFieldOfView) normalFieldOfView = initialFov;
        if (aimFieldOfView >= normalFieldOfView) {
            Debug.LogError("PlayerAimCamera: Aim Field Of View는 일반 FOV보다 작아야 합니다.", this);
            enabled = false;
            return;
        }
        offset = freeLookCamera.GetComponent<CinemachineCameraOffset>();
        if (offset != null && (!offset.enabled || offset.m_ApplyAfter != CinemachineCore.Stage.Body)) {
            Debug.LogError("PlayerAimCamera: 기존 CameraOffset을 활성화하고 Apply After를 Body로 설정하세요.", this);
            enabled = false;
            return;
        }
        CinemachineCollider collision = freeLookCamera.GetComponent<CinemachineCollider>();
        if (collision == null) {
            if (cameraCollisionMask.value == 0) {
                Debug.LogError("PlayerAimCamera: Camera Collision Mask에 장애물 레이어를 지정하세요.", this);
                enabled = false;
                return;
            }
            collision = freeLookCamera.gameObject.AddComponent<CinemachineCollider>();
            collision.m_CollideAgainst = cameraCollisionMask;
            collision.m_CameraRadius = cameraRadius;
            collision.m_AvoidObstacles = true;
            collision.m_DampingWhenOccluded = 0f;
            collision.m_Damping = 0.2f;
            if (!CompareTag("Untagged")) collision.m_IgnoreTag = tag;
        }
        if (!collision.enabled || !collision.m_AvoidObstacles || collision.m_CollideAgainst.value == 0) {
            Debug.LogError("PlayerAimCamera: CinemachineCollider의 충돌 회피와 장애물 레이어를 설정하세요.", this);
            enabled = false;
            return;
        }
        if (offset == null) {
            offset = freeLookCamera.gameObject.AddComponent<CinemachineCameraOffset>();
            offset.m_ApplyAfter = CinemachineCore.Stage.Body;
        }
        // 오프셋을 먼저 적용한 위치에서 충돌을 해결해야 벽 안으로 카메라가 이동하지 않는다.
        freeLookCamera.RemoveExtension(collision);
        freeLookCamera.AddExtension(collision);
        initialOffset = offset.m_Offset;
        ready = true;
        Apply(0f);
    }

    private void OnEnable() {
        if (ready) Apply(0f);
    }

    private void LateUpdate() {
        if (!ready || freeLookCamera == null || offset == null) return;
        if (shooter == null || !shooter.isActiveAndEnabled || playerInput == null || !playerInput.isActiveAndEnabled ||
            (GameManager.instance != null && GameManager.instance.isGameover)) {
            Apply(0f);
            return;
        }
        GameSessionManager session = GameSessionManager.instance;
        if (Time.timeScale == 0f || (session != null && (session.IsPaused || session.IsRebinding))) return;
        float target = playerInput.IsAiming ? 1f : 0f;
        Apply(Mathf.MoveTowards(blend, target, Time.deltaTime / transitionDuration));
    }

    private void Apply(float value) {
        if (freeLookCamera == null || offset == null) return;
        blend = value;
        float weight = Mathf.SmoothStep(0f, 1f, blend);
        freeLookCamera.m_CommonLens = true;
        freeLookCamera.m_Lens.FieldOfView = Mathf.Lerp(normalFieldOfView, aimFieldOfView, weight);
        offset.m_Offset = initialOffset + Vector3.Lerp(normalOffset, aimOffset, weight);
    }

    private void OnDisable() {
        blend = 0f;
        if (!ready) return;
        if (freeLookCamera != null) {
            freeLookCamera.m_Lens.FieldOfView = initialFov;
            freeLookCamera.m_CommonLens = initialCommonLens;
        }
        if (offset != null) offset.m_Offset = initialOffset;
    }

    private void OnValidate() {
        normalFieldOfView = Mathf.Clamp(normalFieldOfView, 1f, 179f);
        aimFieldOfView = Mathf.Clamp(aimFieldOfView, 1f, 179f);
        transitionDuration = Mathf.Max(0.01f, transitionDuration);
        cameraRadius = Mathf.Max(0.01f, cameraRadius);
    }
}
