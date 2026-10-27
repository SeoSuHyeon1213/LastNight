using UnityEngine;

// 체력을 회복하는 아이템
public class HealthPack : MonoBehaviour, IItem, IItemSpawnAmount {
    public float health = 50; // 체력을 회복할 수치
    [SerializeField] private AudioClip pickupClip;
    private bool consumed;

    public void SetSpawnAmount(int amount) { health = Mathf.Max(1, amount); }

    public bool Use(GameObject target) {
        if (consumed || !isActiveAndEnabled || target == null || health <= 0f ||
            float.IsNaN(health) || float.IsInfinity(health)) return false;
        // 전달받은 게임 오브젝트로부터 LivingEntity 컴포넌트 가져오기 시도
        LivingEntity life = target.GetComponent<LivingEntity>();

        // LivingEntity컴포넌트가 있다면
        if (life == null || life.dead || life.health >= life.startingHealth) return false;
        float previousHealth = life.health;
        life.RestoreHealth(Mathf.Min(health, life.startingHealth - life.health));
        if (life.health <= previousHealth) return false;

        // 사용되었으므로, 자신을 파괴
        consumed = true;
        Destroy(gameObject);
        return true;
    }

    public void PlayPickupSound(AudioSource source) {
        if (source != null && source.isActiveAndEnabled && pickupClip != null) source.PlayOneShot(pickupClip);
    }
}
