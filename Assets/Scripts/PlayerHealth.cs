using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI; // UI 관련 코드

// 플레이어 캐릭터의 생명체로서의 동작을 담당
public class PlayerHealth : LivingEntity {
    public Slider healthSlider; // 체력을 표시할 UI 슬라이더
    // 아이템을 사용(획득)하고 획득음을 재생한 직후 호출된다. 소음 발생기가 구독한다.
    public event Action ItemPickedUp;

    public AudioClip deathClip; // 사망 소리
    public AudioClip hitClip; // 피격 소리

    private AudioSource playerAudioPlayer; // 플레이어 소리 재생기
    private Animator playerAnimator; // 플레이어의 애니메이터

    private PlayerMovement playerMovement; // 플레이어 움직임 컴포넌트
    private PlayerShooter playerShooter; // 플레이어 슈터 컴포넌트
    private Rigidbody playerBody;
    private readonly Dictionary<Collider, IItem> pickupContacts = new Dictionary<Collider, IItem>();
    private readonly Dictionary<IItem, int> overlappingItems = new Dictionary<IItem, int>();
    private readonly List<Collider> stalePickupContacts = new List<Collider>();

    private void Awake() {
        if (GetComponent<GrenadeInventory>() == null) gameObject.AddComponent<GrenadeInventory>();
        if (GetComponent<PlankInventory>() == null) gameObject.AddComponent<PlankInventory>();
        if (GetComponent<BarricadeBuilder>() == null) gameObject.AddComponent<BarricadeBuilder>();
        // 사용할 컴포넌트를 가져오기
        playerAnimator = GetComponent<Animator>();
        playerAudioPlayer = GetComponent<AudioSource>();

        playerMovement = GetComponent<PlayerMovement>();
        playerShooter = GetComponent<PlayerShooter>();
        playerBody = GetComponent<Rigidbody>();
    }

    protected override void OnEnable() {
        ClearPickupContacts();
        // LivingEntity의 OnEnable() 실행 (상태 초기화)
        base.OnEnable();

        // 체력 슬라이더 활성화
        healthSlider.gameObject.SetActive(true);
        // 체력 슬라이더의 최대값을 기본 체력값으로 변경
        healthSlider.maxValue = startingHealth;
        // 체력 슬라이더의 값을 현재 체력값으로 변경
        healthSlider.value = health;

        // 플레이어 조작을 받는 컴포넌트들 활성화
        playerMovement.enabled = true;
        playerShooter.enabled = true;
    }

    // 체력 회복
    public override void RestoreHealth(float newHealth) {
        // LivingEntity의 RestoreHealth() 실행 (체력 증가)
        base.RestoreHealth(newHealth);
        // 체력 갱신
        healthSlider.value = health; //갱신된 체력을 체력 슬라이드에 반영
    }


    // 데미지 처리
    public override void OnDamage(float damage, Vector3 hitPoint, Vector3 hitDirection) {
        if (!dead)
        {
            // 사망하지 않은 경우에만 효과음을 재생
            playerAudioPlayer.PlayOneShot(hitClip);
        }

        // LivingEntity의 OnDamage() 실행(데미지 적용)
        base.OnDamage(damage, hitPoint, hitDirection);
        // 갱신된 체력을 체력 슬라이더에 반영
        healthSlider.value = health;
    }

    // 사망 처리
    public override void Die() {
        // 이미 사망했다면 사망 연출·조작 비활성화를 반복하지 않음
        if (dead) return;
        // LivingEntity의 Die() 실행(사망 적용)
        base.Die();

        // 체력 슬라이더 비활성화
        healthSlider.gameObject.SetActive(false);

        // 사망음 재생
        playerAudioPlayer.PlayOneShot(deathClip);
        // 애니메이터의 Die 트리거를 발동시켜 사망 애니메이션 재생
        playerAnimator.SetTrigger("Die");

        // 플레이어 조작을 받는 컴포넌트들 비활성화
        playerMovement.enabled = false; //기능만 비활성화, GameObject.setActive()는 오브젝트의 출력 유무까지 동시에 정함 
        playerShooter.enabled = false;
    }

    private void OnTriggerEnter(Collider other) {
        PrunePickupContacts();
        IItem item = other.GetComponentInParent<IItem>();
        if (item == null) return;
        // 플레이어의 여러 몸 Collider가 같은 아이템에 닿아도 방문당 한 번만 시도한다.
        if (pickupContacts.ContainsKey(other)) return;
        pickupContacts.Add(other, item);
        if (overlappingItems.TryGetValue(item, out int colliderCount)) {
            overlappingItems[item] = colliderCount + 1;
            return;
        }
        overlappingItems.Add(item, 1);
        GameSessionBase session = GameSessionBase.instance;
        if (dead || Time.timeScale <= 0f || (GameManager.instance != null && GameManager.instance.isGameover) ||
            (session != null && (session.IsPaused || session.IsRebinding))) return;
        if (!item.Use(gameObject)) return;
        item.PlayPickupSound(playerAudioPlayer);
        ItemPickedUp?.Invoke();
    }

    private void OnTriggerExit(Collider other) {
        if (!pickupContacts.ContainsKey(other) || StillOverlapsPlayer(other)) return;
        RemovePickupContact(other);
    }

    private bool StillOverlapsPlayer(Collider other) {
        if (other == null || !other.enabled || !other.gameObject.activeInHierarchy) return false;
        // 몸/무기 Collider가 꺼질 때 Exit가 누락돼도 완전히 벗어나면 방문을 해제한다.
        foreach (Collider own in GetComponentsInChildren<Collider>(true)) {
            if (!own.enabled || !own.gameObject.activeInHierarchy || own.attachedRigidbody != playerBody) continue;
            if (Physics.ComputePenetration(own, own.transform.position, own.transform.rotation,
                other, other.transform.position, other.transform.rotation, out _, out _)) return true;
        }
        return false;
    }

    private void RemovePickupContact(Collider other) {
        IItem item = pickupContacts[other];
        pickupContacts.Remove(other);
        int remaining = overlappingItems[item] - 1;
        if (remaining > 0) overlappingItems[item] = remaining;
        else overlappingItems.Remove(item);
    }

    private void PrunePickupContacts() {
        // Collider 파괴 시 Exit가 오지 않을 수 있어 다음 접촉 때 이전 방문을 정리한다.
        stalePickupContacts.Clear();
        foreach (var pair in pickupContacts)
            if (pair.Key == null || !pair.Key.enabled || !pair.Key.gameObject.activeInHierarchy ||
                pair.Value is UnityEngine.Object itemObject && itemObject == null)
                stalePickupContacts.Add(pair.Key);
        foreach (Collider stale in stalePickupContacts) RemovePickupContact(stale);
        stalePickupContacts.Clear();
    }

    private void ClearPickupContacts() {
        pickupContacts.Clear();
        overlappingItems.Clear();
        stalePickupContacts.Clear();
    }

    private void OnDisable() { ClearPickupContacts(); }
}
