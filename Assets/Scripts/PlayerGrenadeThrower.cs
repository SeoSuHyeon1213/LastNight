using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerGrenadeThrower : MonoBehaviour {
    [SerializeField] private Grenade grenadePrefab;
    [SerializeField] private Transform throwOrigin;
    [SerializeField] private Camera aimCamera;
    [SerializeField] private KeyCode throwKey = KeyCode.G;
    [SerializeField, Min(0.01f)] private float spawnClearance = 0.15f;
    [SerializeField] private LayerMask blockingLayers = Physics.DefaultRaycastLayers;

    private GrenadeInventory inventory;
    private PlayerHealth playerHealth;

    private void Awake() {
        inventory = GetComponent<GrenadeInventory>();
        playerHealth = GetComponent<PlayerHealth>();
        if (inventory == null || playerHealth == null || grenadePrefab == null ||
            throwOrigin == null || aimCamera == null) {
            Debug.LogError("PlayerGrenadeThrower: GrenadeInventory, PlayerHealth, Grenade Prefab, Throw Origin, Aim Camera를 연결하세요.", this);
            enabled = false;
        }
    }

    private void Update() {
        if (Application.isFocused && Input.GetKeyDown(throwKey)) TryThrow();
    }

    public bool TryThrow() {
        GameSessionManager session = GameSessionManager.instance;
        if (!isActiveAndEnabled || Time.timeScale <= 0f || inventory == null ||
            playerHealth == null || playerHealth.dead || inventory.Count == 0 ||
            (GameManager.instance != null && GameManager.instance.isGameover) ||
            (session != null && (session.IsPaused || session.IsRebinding))) return false;
        if (grenadePrefab == null || throwOrigin == null || aimCamera == null) return false;

        Vector3 position = throwOrigin.position;
        if (!IsSpawnClear(position)) return false;

        Grenade grenade = Instantiate(grenadePrefab, position, Quaternion.identity);
        grenade.gameObject.SetActive(true);
        grenade.Throw(aimCamera.transform.forward, gameObject);
        if (!grenade.HasBeenThrown) {
            Destroy(grenade.gameObject);
            return false;
        }
        // 생성과 발사에 성공한 경우에만 보유량을 차감한다.
        if (!inventory.TrySpendOne()) {
            Destroy(grenade.gameObject);
            return false;
        }
        return true;
    }

    private bool IsSpawnClear(Vector3 position) {
        foreach (Collider hit in Physics.OverlapSphere(position, spawnClearance,
            blockingLayers, QueryTriggerInteraction.Ignore)) {
            if (!hit.transform.IsChildOf(transform)) return false;
        }
        // 투척 지점이 얇은 벽 너머에 놓여 벽을 통과해 생성되는 것을 막는다.
        Vector3 from = transform.position + Vector3.up * Vector3.Dot(position - transform.position, Vector3.up);
        Vector3 offset = position - from;
        if (offset.sqrMagnitude < 0.0001f) return true;
        foreach (RaycastHit hit in Physics.SphereCastAll(from, spawnClearance, offset.normalized,
            offset.magnitude, blockingLayers, QueryTriggerInteraction.Ignore)) {
            if (!hit.transform.IsChildOf(transform)) return false;
        }
        return true;
    }

    private void OnValidate() {
        spawnClearance = Mathf.Max(0.01f, spawnClearance);
    }
}