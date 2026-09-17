using UnityEngine;

using Cinemachine;

// FreeLook 시점에 맞춰 캐릭터를 회전시키고 카메라 기준으로 이동한다.
public class PlayerMovement : MonoBehaviour {
    public float moveSpeed = 5f;
    [SerializeField, Range(0f, 1f)] private float walkSpeedRatio = 0.5f;
    public float rotateSpeed = 180f;
    [SerializeField] private Transform viewCamera;
    [SerializeField] private CinemachineFreeLook freeLookCamera;
    [Header("Aim Camera")]
    [SerializeField, Range(1f, 179f)] private float aimFieldOfView = 40f;
    [SerializeField, Min(0f)] private float aimTransitionSpeed = 80f;
    private Animator playerAnimator;
    private PlayerInput playerInput;
    private Rigidbody playerRigidbody;
    private Vector3 viewForward;
    private float defaultFieldOfView;
    private bool isCameraConfigured;

    private void Awake() {
        playerInput = GetComponent<PlayerInput>();
        playerRigidbody = GetComponent<Rigidbody>();
        playerAnimator = GetComponent<Animator>();
    }

    private void Start() {
        // Look At 대상이나 FreeLook 루트는 실제 출력 카메라의 회전을 나타내지 않는다.
        if (viewCamera == null || viewCamera.GetComponent<Camera>() == null) {
            if (viewCamera != null)
                Debug.LogWarning("PlayerMovement: View Camera에는 실제 Camera가 필요합니다. Main Camera로 교체합니다.", this);
            Camera mainCamera = Camera.main;
            viewCamera = mainCamera != null ? mainCamera.transform : null;
        }
        if (viewCamera == null || playerInput == null || playerRigidbody == null || playerAnimator == null) {
            Debug.LogError("PlayerMovement: Main Camera, PlayerInput, Rigidbody, Animator 연결을 확인하세요.", this);
            enabled = false;
            return;
        }
        if (freeLookCamera == null) {
            foreach (var candidate in FindObjectsByType<CinemachineFreeLook>(FindObjectsSortMode.None)) {
                if (candidate.Follow != null &&
                    (candidate.Follow == transform || candidate.Follow.IsChildOf(transform))) {
                    if (freeLookCamera != null) {
                        Debug.LogError("PlayerMovement: FreeLook이 여러 개입니다. Free Look Camera를 직접 연결하세요.", this);
                        enabled = false;
                        return;
                    }
                    freeLookCamera = candidate;
                }
            }
        }
        if (freeLookCamera == null) {
            Debug.LogError("PlayerMovement: Free Look Camera를 연결하고 Follow를 플레이어로 설정하세요.", this);
            enabled = false;
            return;
        }
        // 캐릭터의 수평 회전만 허용하고 위치 및 X/Z축 제한은 유지한다.
        playerRigidbody.constraints &= ~RigidbodyConstraints.FreezeRotationY;
        // 몸의 회전이 카메라로 되돌아가 계속 회전하는 현상을 막는다.
        freeLookCamera.m_BindingMode = CinemachineTransposer.BindingMode.WorldSpace;
        freeLookCamera.m_RecenterToTargetHeading.m_enabled = false;
        freeLookCamera.m_YAxisRecentering.m_enabled = false;
        ConnectLookInput();
        defaultFieldOfView = freeLookCamera.m_Lens.FieldOfView;
        isCameraConfigured = true;
        if (aimFieldOfView >= defaultFieldOfView)
            Debug.LogWarning("PlayerMovement: Aim Field Of View는 기본 FOV보다 작아야 확대됩니다.", this);
        viewForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
    }

    private void LateUpdate() {
        if (!isCameraConfigured || freeLookCamera == null)
            return;

        float targetFieldOfView = playerInput != null && playerInput.IsAiming
            ? aimFieldOfView
            : defaultFieldOfView;
        freeLookCamera.m_Lens.FieldOfView = Mathf.MoveTowards(
            freeLookCamera.m_Lens.FieldOfView,
            targetFieldOfView,
            aimTransitionSpeed * Time.deltaTime);
    }

    private void OnDisable() {
        if (isCameraConfigured && freeLookCamera != null) {
            freeLookCamera.m_Lens.FieldOfView = defaultFieldOfView;
            freeLookCamera.UpdateInputAxisProvider();
        }
    }

    private void OnEnable() {
        if (isCameraConfigured && freeLookCamera != null)
            ConnectLookInput();
    }

    private void ConnectLookInput() {
        freeLookCamera.m_XAxis.SetInputAxisProvider(0, playerInput);
        freeLookCamera.m_YAxis.SetInputAxisProvider(1, playerInput);
    }

    private void FixedUpdate() {
        if (viewCamera == null || (GameManager.instance != null && GameManager.instance.isGameover))
            return;
        Vector3 horizontalForward = Vector3.ProjectOnPlane(viewCamera.forward, Vector3.up);
        // 수직에 가까운 시점에서는 마지막 유효한 수평 방향을 유지한다.
        if (horizontalForward.sqrMagnitude > 0.0001f)
            viewForward = horizontalForward.normalized;

        Quaternion targetRotation = Quaternion.LookRotation(viewForward, Vector3.up);
        playerRigidbody.MoveRotation(Quaternion.RotateTowards(
            playerRigidbody.rotation, targetRotation, Mathf.Max(0f, rotateSpeed) * Time.fixedDeltaTime));

        // 기존 Horizontal 입력을 회전 대신 좌우 이동에 사용한다.
        Vector3 viewRight = Vector3.Cross(Vector3.up, viewForward);
        Vector3 direction = Vector3.ClampMagnitude(
            viewForward * playerInput.move + viewRight * playerInput.rotate, 1f);
        // 기존 Move Speed는 달리기 속도이며 일반 이동은 설정된 비율만큼 느리다.
        float currentSpeed = moveSpeed * (playerInput.IsRunning ? 1f : walkSpeedRatio);
        playerRigidbody.MovePosition(playerRigidbody.position + direction * currentSpeed * Time.fixedDeltaTime);

        // Blend Tree의 걷기 좌표는 0.5, 달리기 좌표는 1을 사용한다.
        float animationScale = playerInput.IsRunning ? 1f : 0.5f;
        Vector2 animationInput = Vector2.ClampMagnitude(new Vector2(playerInput.rotate, playerInput.move), 1f);
        playerAnimator.SetFloat("Move", animationInput.y * animationScale);
        playerAnimator.SetFloat("Rotate", animationInput.x * animationScale);
    }
}
