using UnityEngine;

public sealed class PlankPickup : MonoBehaviour, IItem {
    [Min(1)] public int amount = 3;
    private bool consumed;
    public void Use(GameObject target) {
        if (consumed || target == null) return;
        PlankInventory inventory = target.GetComponent<PlankInventory>();
        if (inventory == null) return;
        amount -= inventory.Add(amount);
        if (amount > 0) return;
        consumed = true;
        gameObject.SetActive(false);
        Destroy(gameObject);
    }
}
