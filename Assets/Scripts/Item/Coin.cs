using UnityEngine;

// 게임 점수를 증가시키는 아이템
public class Coin : MonoBehaviour, IItem, IItemSpawnAmount {
    public int score = 200; // 증가할 점수
    [SerializeField] private AudioClip pickupClip;
    private bool consumed;

    public void SetSpawnAmount(int amount) { score = Mathf.Max(1, amount); }

    public bool Use(GameObject target) {
        if (consumed || !isActiveAndEnabled || target == null || score <= 0 ||
            GameManager.instance == null || GameManager.instance.isGameover) return false;
        // 게임 매니저로 접근해 점수 추가
        GameManager.instance.AddScore(score);
        // 사용되었으므로, 자신을 파괴
        consumed = true;
        Destroy(gameObject);
        return true;
    }

    public void PlayPickupSound(AudioSource source) {
        if (source != null && source.isActiveAndEnabled && pickupClip != null) source.PlayOneShot(pickupClip);
    }
}
