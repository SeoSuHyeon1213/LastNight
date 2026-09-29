using UnityEngine;

public sealed class WeaponPickup : MonoBehaviour, IItem {
    private Gun prefab;
    private int slot;
    private int duplicateAmmo;
    private bool consumed;

    public void Initialize(Gun weaponPrefab, int weaponSlot, int ammo) {
        prefab = weaponPrefab;
        slot = weaponSlot;
        duplicateAmmo = ammo;
    }

    public void Use(GameObject target) {
        if (consumed || prefab == null || target == null) return;
        PlayerShooter shooter = target.GetComponent<PlayerShooter>();
        if (shooter == null || !shooter.TryAcquireWeapon(slot, prefab, duplicateAmmo)) return;
        consumed = true;
        GetComponent<Collider>().enabled = false;
        Destroy(gameObject);
    }
}
