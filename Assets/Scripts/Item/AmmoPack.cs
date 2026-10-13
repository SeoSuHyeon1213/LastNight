using UnityEngine;

// 총알을 충전하는 아이템
public class AmmoPack : MonoBehaviour, IItem {
    public int ammo = 30; // 충전할 총알 수
    [SerializeField] private AudioClip pickupClip;
    private bool consumed;

    public bool Use(GameObject target) {
        if (consumed || !isActiveAndEnabled || target == null || ammo <= 0) return false;
        // 전달 받은 게임 오브젝트로부터 PlayerShooter 컴포넌트를 가져오기 시도
        PlayerShooter playerShooter = target.GetComponent<PlayerShooter>();

        // PlayerShooter 컴포넌트가 있으며, 총 오브젝트가 존재하면
        Gun weapon = playerShooter != null ? playerShooter.PrimaryGun : null;
        if (weapon == null || !weapon.UsesAmmo || weapon.HasInfiniteAmmo) return false;
        int previousAmmo = weapon.ammoRemain;
        // 장착 슬롯과 관계없이 주무기의 예비 탄약을 보충한다.
        weapon.AddAmmo(ammo);
        if (weapon.ammoRemain <= previousAmmo) return false;

        // 사용되었으므로, 자신을 파괴
        consumed = true;
        Destroy(gameObject);
        return true;
    }

    public void PlayPickupSound(AudioSource source) {
        if (source != null && source.isActiveAndEnabled && pickupClip != null) source.PlayOneShot(pickupClip);
    }
}
