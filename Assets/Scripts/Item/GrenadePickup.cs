using UnityEngine;

public sealed class GrenadePickup : MonoBehaviour, IItem {
    [Min(1)] public int amount = 1;
    private bool consumed;

    public void Use(GameObject target) {
        if (consumed || target == null || amount <= 0) return;
        GrenadeInventory inventory = target.GetComponent<GrenadeInventory>();
        if (inventory == null) return;
        amount -= inventory.Add(amount);
        // 소지 상한으로 받지 못한 수량은 아이템에 남긴다.
        if (amount > 0) return;
        consumed = true;
        gameObject.SetActive(false);
        Destroy(gameObject);
    }

    private void OnValidate() { amount = Mathf.Max(1, amount); }
}
