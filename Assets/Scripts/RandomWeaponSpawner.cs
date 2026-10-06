using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;


// 5, 10, 15... 웨이브에 Empty 위치에 새 무기를 생성한다.
// 일반 웨이브는 생성 시작 시, 빅웨이브는 준비 진입 시 한 번만 지급하고 전투 시작 때는 지급하지 않는다.
public sealed class RandomWeaponSpawner : MonoBehaviour {
    public enum WeaponSlot { Primary = 1, Secondary = 2 }

    [Serializable]
    public class Entry {
        public Gun prefab;
        public WeaponSlot slot = WeaponSlot.Primary;
        [Min(1)] public int duplicateAmmo = 30;
    }

    public Entry[] weapons;
    public ZombieSpawner waveSpawner;
    public TextMeshProUGUI spawnNotification;
    [SerializeField, Min(1)] private int supplyWaveInterval = 5;
    [SerializeField, Min(0.1f)] private float pickupRadius = 0.8f;
    [SerializeField] private Vector3 displayOffset = new Vector3(0f, 0.5f, 0f);
    private readonly List<Entry> available = new List<Entry>();
    private GameObject currentPickup;
    private int lastSuppliedWave = -1; // 같은 웨이브 중복 지급 방지
    private bool started;
    private bool subscribed;
    private bool isWeaponSpawned = false;
    private Coroutine hideCoroutine;

    private void Start() {
        if (waveSpawner == null) waveSpawner = FindAnyObjectByType<ZombieSpawner>();
        if (waveSpawner == null) {
            Debug.LogError("RandomWeaponSpawner: Wave Spawner를 연결하세요.", this);
            enabled = false;
            return;
        }
        if (spawnNotification == null)
            Debug.LogWarning("RandomWeaponSpawner: Spawn Notification이 없어 생성 알림은 표시되지 않습니다.", this);

        else
        {
            spawnNotification.gameObject.SetActive(false);
        }
        if (weapons != null) {
            foreach (Entry entry in weapons) {
                if (entry == null || entry.prefab == null || entry.prefab.gunData == null ||
                    entry.prefab.fireTransform == null || entry.duplicateAmmo < 1 ||
                    (entry.slot != WeaponSlot.Primary && entry.slot != WeaponSlot.Secondary)) {
                    Debug.LogWarning("RandomWeaponSpawner: 잘못된 생성 항목을 제외합니다.", this);
                    continue;
                }
                if (WeaponGrip.FindMount(entry.prefab, "Left Handle") == null ||
                    WeaponGrip.FindMount(entry.prefab, "Right Handle") == null) continue;
                available.Add(entry);
            }
        }
        if (available.Count == 0) {
            Debug.LogError("RandomWeaponSpawner: 유효한 무기 프리팹을 하나 이상 연결하세요.", this);
            enabled = false;
            return;
        }
        started = true;
        Subscribe();
    }

    private void OnEnable() {
        if (started) Subscribe();
    }

    private void Subscribe() {
        if (subscribed || waveSpawner == null) return;
        waveSpawner.PhaseChanged += HandleWavePhaseChanged;
        subscribed = true;
    }

    private void Unsubscribe() {
        if (!subscribed) return;
        if (waveSpawner != null) waveSpawner.PhaseChanged -= HandleWavePhaseChanged;
        subscribed = false;
    }

    private void HandleWavePhaseChanged(WaveStatus status) {
        if (available.Count == 0 || (GameManager.instance != null && GameManager.instance.isGameover)) return;
        bool supplyMoment = status.IsBigWave ? status.Phase == WavePhase.Preparing : status.Phase == WavePhase.Spawning;
        if (!supplyMoment || status.Wave <= 0 || status.Wave % supplyWaveInterval != 0) return;
        if (status.Wave == lastSuppliedWave) return;
        lastSuppliedWave = status.Wave;
        SupplyWeapon();
    }

    private void SupplyWeapon() {
        // 한 생성기에는 한 자루만 남겨 다음 보급 때 미획득 무기를 교체한다.
        if (currentPickup != null) {
            currentPickup.SetActive(false);
            Destroy(currentPickup);
        }
        Entry entry = available[UnityEngine.Random.Range(0, available.Count)];
        Spawn(entry);
        if(isWeaponSpawned)
        {
            
            
            if (spawnNotification == null) return;

            // 이미 실행 중인 타이머 코루틴이 있다면 중지 (연속 클릭 시 타이머 리셋 효과)
            if (hideCoroutine != null)
            {
                StopCoroutine(hideCoroutine);
            }

             // 텍스트 설정 및 활성화
            spawnNotification.text = "WeaponSpawnd! name is" + currentPickup.name;
            spawnNotification.gameObject.SetActive(true);

            // 3초 뒤 비활성화 코루틴 시작
            hideCoroutine = StartCoroutine(HideTextRoutine(3f));
        }
        
        
    }

    private void Spawn(Entry entry) {
        currentPickup = new GameObject("Weapon Pickup - " + entry.prefab.name);
        currentPickup.SetActive(false);
        currentPickup.transform.SetPositionAndRotation(transform.position + displayOffset, transform.rotation);
        var pickup = currentPickup.AddComponent<WeaponPickup>();
        pickup.Initialize(entry.prefab, (int)entry.slot, entry.duplicateAmmo);
        var trigger = currentPickup.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = pickupRadius;
        var body = currentPickup.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;

        Gun display = Instantiate(entry.prefab, currentPickup.transform, false);
        display.transform.localPosition = Vector3.zero;
        display.enabled = false;
        display.gameObject.SetActive(true);
        // 필드 모델은 발사나 물리 충돌을 수행하지 않고 루트 트리거만 획득을 처리한다.
        foreach (Collider c in display.GetComponentsInChildren<Collider>(true)) c.enabled = false;
        foreach (Rigidbody rb in display.GetComponentsInChildren<Rigidbody>(true)) {
            rb.isKinematic = true;
            rb.detectCollisions = false;
        }
        foreach (AudioSource audio in display.GetComponentsInChildren<AudioSource>(true)) audio.enabled = false;
        foreach (LineRenderer line in display.GetComponentsInChildren<LineRenderer>(true)) line.enabled = false;
        foreach (ParticleSystem effect in display.GetComponentsInChildren<ParticleSystem>(true)) {
            var main = effect.main;
            main.playOnAwake = false;
            effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        isWeaponSpawned = true;
        currentPickup.SetActive(true);

    }

    private void OnDisable() {
        Unsubscribe();
        if (hideCoroutine != null) {
            StopCoroutine(hideCoroutine);
            hideCoroutine = null;
        }
        if (spawnNotification != null) spawnNotification.gameObject.SetActive(false);
        isWeaponSpawned = false;
    }

    private void OnDestroy() {
        if (currentPickup != null) Destroy(currentPickup);
    }

    private IEnumerator HideTextRoutine(float delay)
    {
        isWeaponSpawned = false;
        yield return new WaitForSeconds(delay);

        if(spawnNotification != null)
        {
            spawnNotification.gameObject.SetActive(false);
        }
        hideCoroutine = null;
    }
}
