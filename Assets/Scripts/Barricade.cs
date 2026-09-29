using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(BoxCollider))]
public sealed class Barricade : LivingEntity {
    public GameObject plankPrefab;
    public Transform[] plankSockets;
    [Min(1)] public int healthPerPlank = 20;
    [Min(0.1f)] public float installSeconds = 1.5f;
    private GameObject[] planks;
    private BoxCollider blocker;
    public int PlankCount { get; private set; }
    public int DamageRevision { get; private set; }
    public int Capacity => plankSockets == null ? 0 : plankSockets.Length;
    public float MaxHealth => Capacity * healthPerPlank;
    public Collider Blocker => blocker;
    public bool CanInstall => isActiveAndEnabled && plankPrefab != null && PlankCount < Capacity;
    public bool IsBlocking => isActiveAndEnabled && !dead && PlankCount > 0;
    public Vector3 WorkPosition => transform.position + Vector3.up;
    //public Canvas barricade;
    //public Text barricadeHp;
    
    private void Awake() {
        blocker = GetComponent<BoxCollider>();
        blocker.isTrigger = false;
        if (plankPrefab == null || Capacity == 0) {
            Debug.LogError("Barricade: Plank Prefab과 Plank Sockets를 연결하세요.", this);
            enabled = false;
            blocker.enabled = false;
            return;
        }
        foreach (Transform socket in plankSockets) {
            if (socket == null || !socket.IsChildOf(transform)) {
                Debug.LogError("Barricade: 모든 소켓은 바리케이드의 자식이어야 합니다.", this);
                enabled = false;
                blocker.enabled = false;
                return;
            }
        }
        planks = new GameObject[Capacity];
        for (int i = 0; i < Capacity; i++) {
            planks[i] = Instantiate(plankPrefab, plankSockets[i]);
            planks[i].transform.localPosition = Vector3.zero;
            planks[i].transform.localRotation = Quaternion.identity;
            foreach (Collider c in planks[i].GetComponentsInChildren<Collider>(true)) c.enabled = false;
            planks[i].SetActive(false);
        }
        health = 0;
        dead = true;
        Refresh();
    }

    protected override void OnEnable() {
        // 비활성화 후 복귀할 때 판자와 체력을 초기화하지 않는다.
        if (blocker != null) Refresh();
    }

    private void OnDisable() {
        if (blocker != null) blocker.enabled = false;
    }

    public bool TryInstall(PlankInventory inventory) {
        if (!CanInstall || inventory == null || !inventory.TrySpendOne()) return false;
        PlankCount++;
        dead = false;
        Refresh();
        return true;
    }

    public override void OnDamage(float damage, Vector3 hitPoint, Vector3 hitNormal) {
        if (!IsBlocking || damage <= 0f) return;
        // 한 번의 유효 타격마다 최소 한 조각이 파괴된다.
        int broken = Mathf.Clamp(Mathf.CeilToInt(damage / healthPerPlank), 1, PlankCount);
        DamageRevision++;
        PlankCount -= broken;
        Refresh();
        if (PlankCount == 0) Die();
    }

    public override void Die() {
        if (dead) return;
        PlankCount = 0;
        Refresh();
        base.Die();
    }

    public override void RestoreHealth(float amount) {
        // 재료 소비 없는 회복은 허용하지 않는다. 복구는 TryInstall을 사용한다.
    }

    private void Refresh() {
        health = PlankCount * healthPerPlank;
        if (planks != null)
            for (int i = 0; i < planks.Length; i++) planks[i].SetActive(i < PlankCount);
        blocker.enabled = isActiveAndEnabled && PlankCount > 0;
    }

    private void OnValidate() {
        healthPerPlank = Mathf.Max(1, healthPerPlank);
        installSeconds = Mathf.Max(0.1f, installSeconds);
    }
}
