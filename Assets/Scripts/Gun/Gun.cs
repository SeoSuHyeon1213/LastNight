using System.Collections;
using UnityEngine;

// 총을 구현한다
[RequireComponent(typeof(AudioSource))]
public class Gun : MonoBehaviour {
    // 총의 상태를 표현하는데 사용할 타입을 선언한다
    public enum State {
        Ready, // 발사 준비됨
        Empty, // 탄창이 빔
        Reloading // 재장전 중
    }

    public State state { get; private set; } // 현재 총의 상태

    public Transform fireTransform; // 총알이 발사될 위치

    public ParticleSystem muzzleFlashEffect; // 총구 화염 효과
    public ParticleSystem shellEjectEffect; // 탄피 배출 효과

    private LineRenderer bulletLineRenderer; // 총알 궤적을 그리기 위한 렌더러
    [SerializeField, Min(0.01f)] private float shotEffectDuration = 0.03f;
    private int shotEffectVersion;
    protected float ShotEffectDuration => shotEffectDuration;
    protected virtual bool UsesSingleShotLine => true;

    [SerializeField] private AudioSource gunAudioPlayer; // 비워두면 같은 오브젝트의 출력을 사용

    public GunData gunData; // 총의 현재 데이터
    
    [SerializeField, Min(0.1f)] private float fireDistance = 50f;
    private PlayerAimController aimController;
    public float FireDistance => fireDistance;
    // 예비 탄약의 무한 여부. 탄창은 모든 무기에서 소모한다.
    public virtual bool HasInfiniteAmmo => false;
    public virtual bool UsesAmmo => true;

    public void SetAimController(PlayerAimController controller) {
        aimController = controller;
    }

    public int ammoRemain = 100; // 남은 전체 탄약
    public int magAmmo; // 현재 탄창에 남아있는 탄약
    
    private float lastFireTime; // 총을 마지막으로 발사한 시점
    // 실제로 발사된 시각. 소음 발생기가 발사 여부를 감지하는 데 사용한다.
    public float LastFireTime => lastFireTime;
    private bool ammoInitialized;
    
    protected virtual void Awake() {
        // 사용할 컴포넌트들의 참조를 가져오기
        if (gunAudioPlayer == null) gunAudioPlayer = GetComponent<AudioSource>();
        // 이미 만들어진 프리팹에는 RequireComponent가 소급 적용되지 않는다.
        if (gunAudioPlayer == null) gunAudioPlayer = gameObject.AddComponent<AudioSource>();
        gunAudioPlayer.playOnAwake = false;
        bulletLineRenderer = GetComponent<LineRenderer>();

        // 사용할 점을 두개로 변경
        if (bulletLineRenderer != null) {
            bulletLineRenderer.positionCount = 2;
            bulletLineRenderer.useWorldSpace = true;
            bulletLineRenderer.enabled = false;
        }
    }

    protected virtual void OnEnable() {
        if (!ammoInitialized && gunData != null) {
            ammoRemain = gunData.startAmmoRemain;
            magAmmo = gunData.magCapacity;
            ammoInitialized = true;
            state = State.Ready;
            lastFireTime = 0;
        } else if (state == State.Reloading) {
            state = magAmmo > 0 ? State.Ready : State.Empty;
        }
    }

    // 발사 시도
    public virtual void Fire() {
        if (!isActiveAndEnabled || gunData == null || fireTransform == null) return;
        // 현재 상태가 발사 가능한 상태
        // && 마지막 총 발사 시점에서 timeBetFire 이상의 시간이 지남
        if (state == State.Ready && Time.time >= lastFireTime + gunData.timeBetFire)
        {
            // 실제 발사 처리 실행
            Shot();
        }
    }

    // 실제 발사 처리
    private void Shot() {
        // 레이캐스트에 의한 충돌 정보를 저장하는 컨테이너
        RaycastHit hit;
        // 총알이 맞은 곳을 저장할 변수
        Vector3 hitPosition = Vector3.zero;

        // 레이캐스트(시작지점, 방향, 충돌 정보 컨테이너, 사정거리)
        bool hasHit;
        if (aimController != null) {
            if (!aimController.TryGetShot(out hit, out hasHit, out hitPosition)) return;
        }
        else {
            hasHit = Physics.Raycast(fireTransform.position, fireTransform.forward, out hit,
                fireDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        }
        hitPosition = ResolveShot(hasHit, hit, hitPosition);

        lastFireTime = Time.time;
        // 발사 이펙트 재생 시작
        StartCoroutine(ShotEffect(hitPosition));

        ConsumeAmmo();
    }

    // 조준·총구 차단 검사를 통과한 뒤 무기별 피격 처리만 확장한다.
    protected virtual Vector3 ResolveShot(bool hasHit, RaycastHit hit, Vector3 hitPosition) {
        if (hasHit) {
            IDamageable target = hit.collider.GetComponent<IDamageable>();
            if (target != null) target.OnDamage(gunData.damage, hit.point, hit.normal);
            return hit.point;
        }
        return aimController != null ? hitPosition :
            fireTransform.position + fireTransform.forward * fireDistance;
    }

    protected RaycastHit[] GetOrderedShotHits() {
        if (aimController != null) return aimController.GetOrderedShotHits();
        RaycastHit[] hits = Physics.RaycastAll(fireTransform.position, fireTransform.forward,
            fireDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        hits = System.Array.FindAll(hits, h => !h.collider.transform.IsChildOf(transform));
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        return hits;
    }

    // 파생 무기의 산탄도 기존 조준 마스크와 소유자 제외 규칙을 사용한다.
    protected bool TryGetDirectionalShotHit(Vector3 direction, out RaycastHit hit) {
        if (aimController != null) return aimController.TryGetDirectionalShotHit(direction, out hit);
        hit = default;
        float nearest = float.PositiveInfinity;
        foreach (RaycastHit candidate in Physics.RaycastAll(fireTransform.position, direction,
            fireDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) {
            if (candidate.collider.transform.IsChildOf(transform) || candidate.distance >= nearest) continue;
            hit = candidate;
            nearest = candidate.distance;
        }
        return nearest < float.PositiveInfinity;
    }

    protected virtual void ConsumeAmmo() {
        magAmmo--;
        if (magAmmo <= 0) state = State.Empty;
    }

    public virtual void AddAmmo(int amount) {
        if (amount <= 0) return;
        ammoRemain = (int)System.Math.Min(int.MaxValue, (long)ammoRemain + amount);
    }

    // 발사 이펙트와 소리를 재생하고 총알 궤적을 그린다
    private IEnumerator ShotEffect(Vector3 hitPosition) {
        int version = ++shotEffectVersion;
        // 총구 화염 효과 재생
        if (muzzleFlashEffect != null) muzzleFlashEffect.Play();
        // 탄피 배출 효과 재생
        if (shellEjectEffect != null) shellEjectEffect.Play();

        // 총격 소리 재생
        PlaySound(gunData.shotClip);

        if (!UsesSingleShotLine || bulletLineRenderer == null) yield break;

        // 선의 시작점은 총구의 위치
        bulletLineRenderer.SetPosition(0, fireTransform.position);
        // 선의 끝점은 입력으로 들어온 충돌 위치
        bulletLineRenderer.SetPosition(1, hitPosition);
        // 라인 렌더러를 활성화하여 총알 궤적을 그린다
        bulletLineRenderer.enabled = true;

        // 0.03초 동안 잠시 처리를 대기
        yield return new WaitForSeconds(shotEffectDuration);

        // 라인 렌더러를 비활성화하여 총알 궤적을 지운다
        if (version == shotEffectVersion) bulletLineRenderer.enabled = false;
    }

    // 재장전 시도
    public virtual bool Reload() {
        if (!isActiveAndEnabled || gunData == null) return false;
        if (state == State.Reloading ||
            (!HasInfiniteAmmo && ammoRemain <= 0) || magAmmo >= gunData.magCapacity)
        {
            // 이미 재장전 중이거나, 남은 총알이 없거나
            // 탄창에 총알이 이미 가득한 경우 재장전 할수 없다
            return false;
        }

        // 재장전 처리 시작
        StartCoroutine(ReloadRoutine());
        return true;
    }

    // 실제 재장전 처리를 진행
    private IEnumerator ReloadRoutine() {
        // 현재 상태를 재장전 중 상태로 전환
        state = State.Reloading;
        // 재장전 소리 재생
        PlaySound(gunData.reloadClip);

        // 재장전 소요 시간 만큼 처리를 쉬기
        yield return new WaitForSeconds(gunData.reloadTime);

        // 탄창에 채울 탄약을 계산한다
        int ammoToFill = gunData.magCapacity - magAmmo;

        // 탄창에 채워야할 탄약이 남은 탄약보다 많다면,
        // 채워야할 탄약 수를 남은 탄약 수에 맞춰 줄인다
        if (!HasInfiniteAmmo && ammoRemain < ammoToFill)
        {
            ammoToFill = ammoRemain;
        }

        // 탄창을 채운다
        magAmmo += ammoToFill;
        // 남은 탄약에서, 탄창에 채운만큼 탄약을 뺸다
        if (!HasInfiniteAmmo) ammoRemain -= ammoToFill;

        // 총의 현재 상태를 발사 준비된 상태로 변경
        state = State.Ready;
    }

    protected virtual void OnDisable() {
        StopAllCoroutines();
        shotEffectVersion++;
        if (muzzleFlashEffect != null)
            muzzleFlashEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (shellEjectEffect != null)
            shellEjectEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (state == State.Reloading) state = magAmmo > 0 ? State.Ready : State.Empty;
        if (gunAudioPlayer != null) gunAudioPlayer.Stop();
        if (bulletLineRenderer != null) bulletLineRenderer.enabled = false;
    }

    private void PlaySound(AudioClip clip) {
        if (clip != null && gunAudioPlayer != null && gunAudioPlayer.isActiveAndEnabled)
            gunAudioPlayer.PlayOneShot(clip);
    }
}
