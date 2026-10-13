using UnityEngine;

public sealed class WeaponPickup : MonoBehaviour, IItem {
    private Gun prefab;
    private int slot;
    private int duplicateAmmo;
    [SerializeField] private AudioClip pickupClip;
    private bool consumed;

    public void Initialize(Gun weaponPrefab, int weaponSlot, int ammo, AudioClip pickupSound = null) {
        prefab = weaponPrefab;
        slot = weaponSlot;
        duplicateAmmo = ammo;
        if (pickupSound != null) pickupClip = pickupSound;
    }

    public bool Use(GameObject target) {
        if (consumed || !isActiveAndEnabled || prefab == null || target == null) return false;
        PlayerShooter shooter = target.GetComponent<PlayerShooter>();
        if (shooter == null || !shooter.TryAcquireWeapon(slot, prefab, duplicateAmmo)) return false;
        consumed = true;
        GetComponent<Collider>().enabled = false;
        Destroy(gameObject);
        return true;
    }

    public void PlayPickupSound(AudioSource source) {
        if (source != null && source.isActiveAndEnabled && pickupClip != null) source.PlayOneShot(pickupClip);
    }
}
