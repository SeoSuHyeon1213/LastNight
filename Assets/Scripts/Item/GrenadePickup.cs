using UnityEngine;

public sealed class GrenadePickup : MonoBehaviour, IItem {
    [Min(1)] public int amount = 1;
    [SerializeField] private AudioClip pickupClip;
    private bool consumed;

    public bool Use(GameObject target) {
        if (consumed || !isActiveAndEnabled || target == null || amount <= 0) return false;
        GrenadeInventory inventory = target.GetComponent<GrenadeInventory>();
        if (inventory == null) return false;
        int accepted = inventory.Add(amount);
        if (accepted <= 0) return false;
        amount -= accepted;
        // 소지 상한으로 받지 못한 수량은 아이템에 남긴다.
        if (amount > 0) return true;
        consumed = true;
        gameObject.SetActive(false);
        Destroy(gameObject);
        return true;
    }

    public void PlayPickupSound(AudioSource source) {
        if (source != null && source.isActiveAndEnabled && pickupClip != null) source.PlayOneShot(pickupClip);
    }

    private void OnValidate() { amount = Mathf.Max(1, amount); }
}
