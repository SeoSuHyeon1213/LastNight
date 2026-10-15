using UnityEngine;

[RequireComponent(typeof(PlankInventory), typeof(PlayerHealth))]
public sealed class BarricadeBuilder : MonoBehaviour {
    [SerializeField, Min(0.1f)] private float reach = 2.5f;
    [SerializeField] private LayerMask blockingLayers = Physics.DefaultRaycastLayers;
    private PlankInventory inventory;
    private PlayerHealth player;
    private PlayerInput playerInput;
    private Barricade target;
    private float progress;
    private bool installing;
    private int damageRevision;
    private float nextSearch;
    private Barricade[] sites = System.Array.Empty<Barricade>();
    public Barricade Target => target;
    public bool IsInstalling => installing;
    public string InstallKeyLabel => playerInput != null ? playerInput.InteractLabel : string.Empty;
    public string FormatInstallPrompt(string action) => playerInput != null ? playerInput.FormatInteractPrompt(action) : action;
    public float Progress => target == null ? 0f : progress / target.installSeconds;
    public int Materials => inventory == null ? 0 : inventory.Count;
    public bool CanShowPrompt => isActiveAndEnabled && player != null && !player.dead &&
        target != null && target.isActiveAndEnabled && Time.timeScale > 0f && Application.isFocused &&
        (target.WorkPosition - (transform.position + Vector3.up)).sqrMagnitude <= reach * reach &&
        (GameManager.instance == null || !GameManager.instance.isGameover) &&
        (GameSessionBase.instance == null ||
            (!GameSessionBase.instance.IsPaused && !GameSessionBase.instance.IsRebinding));

    private void Awake() {
        inventory = GetComponent<PlankInventory>();
        player = GetComponent<PlayerHealth>();
        playerInput = GetComponent<PlayerInput>();
        if (playerInput == null) {
            Debug.LogError("BarricadeBuilder: 같은 오브젝트에 PlayerInput이 필요합니다.", this);
            enabled = false;
        }
    }

    private void Update() {
        GameSessionBase session = GameSessionBase.instance;
        if (player.dead || !Application.isFocused ||
            (GameManager.instance != null && GameManager.instance.isGameover) ||
            (session != null && session.IsRebinding)) { Cancel(); return; }
        if (Time.timeScale <= 0f || (session != null && session.IsPaused)) {
            ResetWork();
            return;
        }
        if (Time.time >= nextSearch) {
            sites = FindObjectsByType<Barricade>(FindObjectsSortMode.None);
            nextSearch = Time.time + 0.5f;
        }
        RefreshTarget();
        if (target == null || !target.CanInstall || inventory.Count < 1) {
            ResetWork();
            return;
        }
        if (!installing) {
            if (!playerInput.InteractPressed) return;
            installing = true;
        }
        progress += Time.deltaTime;
        if (progress < target.installSeconds) return;
        target.TryInstall(inventory);
        ResetWork();
    }

    private void RefreshTarget() {
        Barricade nearest = null;
        float distance = reach * reach;
        Vector3 eye = transform.position + Vector3.up;
        foreach (Barricade site in sites) {
            if (site == null || !site.isActiveAndEnabled) continue;
            Vector3 delta = site.WorkPosition - eye;
            if (delta.sqrMagnitude > distance) continue;
            bool blocked = false;
            foreach (RaycastHit hit in Physics.RaycastAll(eye, delta.normalized, delta.magnitude,
                blockingLayers, QueryTriggerInteraction.Ignore)) {
                if (hit.transform.IsChildOf(transform) || hit.transform.IsChildOf(site.transform)) continue;
                blocked = true;
                break;
            }
            if (blocked) continue;
            nearest = site;
            distance = delta.sqrMagnitude;
        }
        if (target != nearest || (target != null && damageRevision != target.DamageRevision)) {
            target = nearest; ResetWork();
            damageRevision = target == null ? 0 : target.DamageRevision;
        }
    }

    private void ResetWork() { installing = false; progress = 0f; }
    private void Cancel() { target = null; ResetWork(); }
    private void OnDisable() { Cancel(); }
}
