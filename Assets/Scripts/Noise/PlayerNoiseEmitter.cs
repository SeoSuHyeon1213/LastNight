using UnityEngine;

// 플레이어의 사격·이동·재장전·아이템 획득·바리케이드 작업 소음을 NoiseDispatcher에 알린다.
// 사격·재장전은 무기 상태 변화로 감지하므로 무기 교체 시 구독을 다시 연결할 필요가 없다.
// 모든 소음은 일시정지·사망·게임오버 중에는 발생하지 않는다.
public sealed class PlayerNoiseEmitter : MonoBehaviour {
    [SerializeField] private NoiseProfile noiseProfile;
    [SerializeField] private NoiseDispatcher noiseDispatcher;
    [SerializeField] private SpawnLocationValidator areaValidator;
    [Tooltip("실내 영역 트리거가 속한 레이어. 비워 두면 항상 실외로 본다.")]
    [SerializeField] private LayerMask indoorZoneLayers = 0;
    [SerializeField, Min(0f)] private float moveInputThreshold = 0.1f;
    [SerializeField, Min(0f)] private float noiseHeight = 1f;

    private PlayerInput playerInput;
    private PlayerShooter playerShooter;
    private PlayerHealth playerHealth;
    private BarricadeBuilder barricadeBuilder; // PlayerHealth.Awake가 붙이므로 Start에서 확보

    private Gun observedGun;
    private float observedFireTime;
    private float nextGunshotTime;
    private float nextMoveNoiseTime;
    private bool observedReloading;
    private float nextWorkNoiseTime;
    private bool subscribed;

    private void Awake() {
        playerInput = GetComponent<PlayerInput>();
        playerShooter = GetComponent<PlayerShooter>();
        playerHealth = GetComponent<PlayerHealth>();
        if (noiseProfile == null || noiseDispatcher == null || playerInput == null) {
            Debug.LogError("PlayerNoiseEmitter: Noise Profile, Noise Dispatcher, 같은 오브젝트의 PlayerInput이 필요합니다.", this);
            enabled = false;
            return;
        }
        if (playerShooter == null)
            Debug.LogWarning("PlayerNoiseEmitter: PlayerShooter가 없어 사격 소음은 발생하지 않습니다.", this);
    }

    private void Start() {
        barricadeBuilder = GetComponent<BarricadeBuilder>();
        if (barricadeBuilder == null)
            Debug.LogWarning("PlayerNoiseEmitter: BarricadeBuilder가 없어 바리케이드 작업 소음은 발생하지 않습니다.", this);
    }

    private void OnEnable() {
        observedGun = null;
        observedFireTime = 0f;
        observedReloading = false;
        nextGunshotTime = 0f;
        nextMoveNoiseTime = 0f;
        nextWorkNoiseTime = 0f;
        if (playerHealth != null && !subscribed) {
            playerHealth.ItemPickedUp += HandleItemPickedUp;
            subscribed = true;
        }
    }

    private void OnDisable() {
        if (playerHealth != null && subscribed) playerHealth.ItemPickedUp -= HandleItemPickedUp;
        subscribed = false;
    }

    private bool CanEmitNow =>
        Time.timeScale > 0f && (playerHealth == null || !playerHealth.dead) &&
        (GameManager.instance == null || !GameManager.instance.isGameover);

    private void Update() {
        if (!CanEmitNow) return;
        TrackGunshot();
        TrackReload();
        TrackMovement();
        TrackBarricadeWork();
    }

    // 첫 발은 즉시, 연사 중에는 반복 간격마다 한 번만 알린다.
    private void TrackGunshot() {
        Gun gun = playerShooter != null ? playerShooter.gun : null;
        if (gun != observedGun) {
            observedGun = gun;
            observedFireTime = gun != null ? gun.LastFireTime : 0f;
            observedReloading = gun != null && gun.state == Gun.State.Reloading;
            return;
        }
        if (gun == null || gun.LastFireTime == observedFireTime) return;
        observedFireTime = gun.LastFireTime;
        if (Time.time < nextGunshotTime) return;

        NoiseProfile.NoiseSetting setting = noiseProfile.Gunshot;
        if (Emit(setting, NoiseKind.Gunshot)) nextGunshotTime = Time.time + setting.repeatInterval;
    }

    // 재장전 상태로 바뀌는 순간 1회. 같은 재장전 중에는 다시 알리지 않는다.
    private void TrackReload() {
        Gun gun = playerShooter != null ? playerShooter.gun : null;
        bool reloading = gun != null && gun.state == Gun.State.Reloading;
        if (gun != observedGun) return; // 무기 교체 프레임은 TrackGunshot이 기준을 갱신한다
        if (reloading && !observedReloading) Emit(noiseProfile.Reload, NoiseKind.Reload);
        observedReloading = reloading;
    }

    // 작업 중에만 반복 간격마다 알린다. 취소·완료·일시정지로 작업이 끝나면 바로 멈춘다.
    // 간격 안에 작업을 다시 시작해도 바로 또 내지 않는다.
    private void TrackBarricadeWork() {
        if (barricadeBuilder == null || !barricadeBuilder.IsInstalling) return;
        if (Time.time < nextWorkNoiseTime) return;
        NoiseProfile.NoiseSetting setting = noiseProfile.BarricadeWork;
        if (Emit(setting, NoiseKind.BarricadeWork)) nextWorkNoiseTime = Time.time + setting.repeatInterval;
    }

    // 획득(회복약 즉시 사용 포함) 1회. 기존 획득음과 같은 시점이다.
    private void HandleItemPickedUp() {
        if (!isActiveAndEnabled || !CanEmitNow) return;
        Emit(noiseProfile.ItemPickup, NoiseKind.ItemPickup);
    }

    private void TrackMovement() {
        if (Time.time < nextMoveNoiseTime) return;
        bool moving = new Vector2(playerInput.move, playerInput.rotate).sqrMagnitude >
            moveInputThreshold * moveInputThreshold;
        NoiseKind kind = playerInput.IsAiming ? NoiseKind.AimMovement : NoiseKind.Movement;
        NoiseProfile.NoiseSetting setting = !moving ? noiseProfile.Stationary
            : kind == NoiseKind.AimMovement ? noiseProfile.AimMovement : noiseProfile.Movement;
        if (Emit(setting, kind)) nextMoveNoiseTime = Time.time + setting.repeatInterval;
    }

    private bool Emit(NoiseProfile.NoiseSetting setting, NoiseKind kind) {
        Vector3 position = transform.position + Vector3.up * noiseHeight;
        float radius = setting.GetRadius(IsIndoors(position));
        if (radius <= 0f) return false;
        noiseDispatcher.Emit(new NoiseEvent(position, radius, kind));
        return true;
    }

    private bool IsIndoors(Vector3 position) {
        return (areaValidator != null && areaValidator.IsIndoors(position)) || (indoorZoneLayers.value != 0 &&
            Physics.CheckSphere(position, 0.1f, indoorZoneLayers, QueryTriggerInteraction.Collide));
    }

    private void OnDrawGizmosSelected() {
        if (noiseProfile == null) return;
        Vector3 position = transform.position + Vector3.up * noiseHeight;
        Gizmos.color = new Color(1f, 0f, 0f, 0.6f);
        Gizmos.DrawWireSphere(position, noiseProfile.Gunshot.radius);
        Gizmos.color = new Color(1f, 0.92f, 0f, 0.6f);
        Gizmos.DrawWireSphere(position, noiseProfile.Movement.radius);
        Gizmos.color = new Color(0f, 1f, 0f, 0.6f);
        Gizmos.DrawWireSphere(position, noiseProfile.AimMovement.radius);
        Gizmos.color = new Color(0.3f, 0.5f, 1f, 0.6f);
        Gizmos.DrawWireSphere(position, noiseProfile.Reload.radius);
        Gizmos.color = new Color(0.7f, 0.3f, 1f, 0.6f);
        Gizmos.DrawWireSphere(position, noiseProfile.BarricadeWork.radius);
    }
}
