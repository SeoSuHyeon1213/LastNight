using UnityEngine;

public sealed class PlankPickup : MonoBehaviour, IItem, IItemSpawnAmount {
    [Min(1)] public int amount = 3;
    [SerializeField] private AudioClip pickupClip;
    private bool consumed;
    public void SetSpawnAmount(int amount) { this.amount = Mathf.Max(1, amount); }
    public bool Use(GameObject target) {
        if (consumed || !isActiveAndEnabled || target == null || amount <= 0) return false;
        PlankInventory inventory = target.GetComponent<PlankInventory>();
        if (inventory == null) return false;
        int accepted = inventory.Add(amount);
        if (accepted <= 0) return false;
        amount -= accepted;
        if (amount > 0) return true;
        consumed = true;
        gameObject.SetActive(false);
        Destroy(gameObject);
        return true;
    }

    public void PlayPickupSound(AudioSource source) {
        if (source != null && source.isActiveAndEnabled && pickupClip != null) source.PlayOneShot(pickupClip);
    }
}
