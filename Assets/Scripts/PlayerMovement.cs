using Cinemachine;
using UnityEngine;

public class PlayerMovement : MonoBehaviour {
    public float moveSpeed = 5f;
    [SerializeField, Min(1f)] private float runSpeedMultiplier = 2f;
    public float rotateSpeed = 180f;
    [SerializeField] private Camera viewCamera;
    [SerializeField] private CinemachineFreeLook freeLookCamera;
    private PlayerInput playerInput;
    private Rigidbody playerRigidbody;
    private Animator playerAnimator;
    private Vector3 viewForward;
    private Transform originalLookAt;
    private Transform stableLookAt;
    private bool originalRootMotion;
    private bool initialized;
    internal CinemachineFreeLook FreeLookCamera => freeLookCamera;
    private static readonly int MoveId = Animator.StringToHash("Move");
    private static readonly int StrafeId = Animator.StringToHash("Rotate");
    private static readonly int MovementSpeedId = Animator.StringToHash("MovementSpeed");

    private void Awake() {
        playerInput = GetComponent<PlayerInput>();
        playerRigidbody = GetComponent<Rigidbody>();
        playerAnimator = GetComponent<Animator>();
        if (viewCamera == null) viewCamera = Camera.main;
        if (playerInput == null || playerRigidbody == null || playerAnimator == null || viewCamera == null) {
            Debug.LogError("PlayerMovement: PlayerInput, Rigidbody, Animator, View Camera를 확인하세요.", this);
            enabled = false;
            return;
        }
        if (freeLookCamera == null) {
            foreach (var camera in FindObjectsByType<CinemachineFreeLook>(FindObjectsSortMode.None)) {
                if (camera.Follow != transform && (camera.Follow == null || !camera.Follow.IsChildOf(transform))) continue;
                if (freeLookCamera != null) {
                    Debug.LogError("PlayerMovement: FreeLook이 여러 개입니다. 직접 연결하세요.", this);
                    enabled = false;
                    return;
                }
                freeLookCamera = camera;
            }
        }
        if (freeLookCamera != null) {
            // 몸의 회전이 카메라 회전으로 되돌아오는 순환을 막는다.
            freeLookCamera.m_BindingMode = CinemachineTransposer.BindingMode.WorldSpace;
            freeLookCamera.m_RecenterToTargetHeading.m_enabled = false;
            freeLookCamera.m_YAxisRecentering.m_enabled = false;
            originalLookAt = freeLookCamera.LookAt;
            if (originalLookAt != null && originalLookAt.IsChildOf(transform)) {
                // 옆으로 떨어진 자식 LookAt은 몸 회전에 따라 이동하여 카메라와 회전 피드백을 만든다.
                float height = transform.InverseTransformPoint(originalLookAt.position).y;
                stableLookAt = new GameObject("Stable Camera LookAt").transform;
                stableLookAt.SetParent(transform, false);
                stableLookAt.localPosition = new Vector3(0f, height, 0f);
                freeLookCamera.LookAt = stableLookAt;
            }
        }
        originalRootMotion = playerAnimator.applyRootMotion;
        // 이동과 회전은 Rigidbody가 소유한다. 애니메이션이 같은 Transform을 다시 움직이지 않게 한다.
        playerAnimator.applyRootMotion = false;
        initialized = true;
        playerRigidbody.constraints &= ~RigidbodyConstraints.FreezeRotationY;
        viewForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
    }

    private void FixedUpdate() {
        if (viewCamera == null || !playerInput.isActiveAndEnabled ||
            (GameManager.instance != null && GameManager.instance.isGameover)) return;
        Vector3 projected = Vector3.ProjectOnPlane(viewCamera.transform.forward, Vector3.up);
        if (projected.sqrMagnitude > 0.0001f) viewForward = projected.normalized;
        Vector2 input = Vector2.ClampMagnitude(new Vector2(playerInput.rotate, playerInput.move), 1f);
        Vector3 direction = viewForward * input.y + Vector3.Cross(Vector3.up, viewForward) * input.x;
        float speed = moveSpeed * (playerInput.IsRunning ? runSpeedMultiplier : 1f);
        playerRigidbody.MovePosition(playerRigidbody.position + direction * speed * Time.fixedDeltaTime);
        playerRigidbody.MoveRotation(Quaternion.RotateTowards(playerRigidbody.rotation,
            Quaternion.LookRotation(viewForward), rotateSpeed * Time.fixedDeltaTime));
        // 대각선에서도 걷기/달리기의 Blend Tree 반경을 각각 0.5/1로 유지한다.
        float animationScale = playerInput.IsRunning ? 1f : 0.5f;
        playerAnimator.SetFloat(MoveId, input.y * animationScale);
        playerAnimator.SetFloat(StrafeId, input.x * animationScale);
        playerAnimator.SetFloat(MovementSpeedId, input.magnitude * animationScale);
    }

    private void OnValidate() {
        moveSpeed = Mathf.Max(0f, moveSpeed);
        runSpeedMultiplier = Mathf.Max(1f, runSpeedMultiplier);
        rotateSpeed = Mathf.Max(0f, rotateSpeed);
    }

    private void OnEnable() {
        if (!initialized) return;
        playerAnimator.applyRootMotion = false;
        if (freeLookCamera != null && stableLookAt != null)
            freeLookCamera.LookAt = stableLookAt;
    }

    private void OnDisable() {
        if (!initialized) return;
        if (playerAnimator != null) playerAnimator.applyRootMotion = originalRootMotion;
        if (freeLookCamera != null && freeLookCamera.LookAt == stableLookAt)
            freeLookCamera.LookAt = originalLookAt;
    }

    private void OnDestroy() {
        if (stableLookAt != null) Destroy(stableLookAt.gameObject);
    }
}
