using UnityEngine;

// 총알을 충전하는 아이템
public class AmmoPack : MonoBehaviour, IItem {
    public int ammo = 30; // 충전할 총알 수

    public void Use(GameObject target) {
        // 전달 받은 게임 오브젝트로부터 PlayerShooter 컴포넌트를 가져오기 시도
        PlayerShooter playerShooter = target.GetComponent<PlayerShooter>();

        // PlayerShooter 컴포넌트가 있으며, 총 오브젝트가 존재하면
        if (playerShooter != null && playerShooter.PrimaryGun != null)
        {
            // 장착 슬롯과 관계없이 주무기의 예비 탄약을 보충한다.
            playerShooter.PrimaryGun.AddAmmo(ammo);
        }

        // 사용되었으므로, 자신을 파괴
        Destroy(gameObject);
    }
}