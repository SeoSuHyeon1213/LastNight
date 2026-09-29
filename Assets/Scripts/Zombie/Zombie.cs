using System.Collections;
using UnityEngine;
using UnityEngine.AI; // AI, 내비게이션 시스템 관련 코드를 가져오기

// 좀비 AI 구현 //부모 클래스가 monobehavier 클래스를 상속 받기 때문에 단일 상속 상태 임에도 컴포넌트 기능 사용 가능
public class Zombie : LivingEntity {
    public LayerMask whatIsTarget; // 추적 대상 레이어

    private Barricade blockingBarricade;
    private LivingEntity targetEntity; // 추적할 대상
    private NavMeshAgent navMeshAgent; // 경로계산 AI 에이전트

    public ParticleSystem hitEffect; // 피격시 재생할 파티클 효과
    public AudioClip deathSound; // 사망시 재생할 소리
    public AudioClip hitSound; // 피격시 재생할 소리

    private Animator zombieAnimator; // 애니메이터 컴포넌트
    private AudioSource zombieAudioPlayer; // 오디오 소스 컴포넌트
    private Renderer zombieRenderer; // 렌더러 컴포넌트

    public float damage = 20f; // 공격력
    public float timeBetAttack = 1f; // 공격 간격
    private float lastAttackTime; // 마지막 공격 시점

    [SerializeField, Min(0.01f)] private float attackReach = 1.5f;
    [SerializeField] private LayerMask attackBlockingLayers = Physics.DefaultRaycastLayers;
    [SerializeField, Min(0f)] private float attackHeight = 1f;
    private bool agentUpdatesRotation;
    private Coroutine pathRoutine;
    private static readonly int AttackTrigger = Animator.StringToHash("HasAttack");

    private LivingEntity currentAttackTarget;
    private Collider currentAttackCollider;

    private bool isAttacking;
    private bool hitApplied;

    // 추적할 대상이 존재하는지 알려주는 프로퍼티
    private bool hasTarget
    {
        get
        {
            // 추적할 대상이 존재하고, 대상이 사망하지 않았다면 true
            if (targetEntity != null && !targetEntity.dead)
            {
                return true;
            }

            // 그렇지 않다면 false
            return false;
        }
    }

    private void Awake() {
        // 게임 오브젝트로부터 사용할 컴포넌트들을 가져오기
        navMeshAgent = GetComponent<NavMeshAgent>();
        zombieAnimator = GetComponent<Animator>();
        // Navigation owns position; attack clips must not move the GameObject.
        zombieAnimator.applyRootMotion = false;
        agentUpdatesRotation = navMeshAgent.updateRotation;
        zombieAudioPlayer = GetComponent<AudioSource>();

        // 렌더러 컴포넌트는 자식 게임 오브젝트에게 있으므로
        // GetComponentInChildren() 메서드를 사용
        zombieRenderer = GetComponentInChildren<Renderer>();
    }

    // 좀비 AI의 초기 스펙을 결정하는 셋업 메서드
    public void Setup(ZombieData zombieData) {
        // 체력 설정
        startingHealth = zombieData.health;
        health = startingHealth;
        // 공격력 설정
        damage = zombieData.damage;
        // 내비메시 에이전트의 이동 속도 설정
        navMeshAgent.speed = zombieData.speed;
        // 렌더러가 사용중인 머테리얼의 컬러를 변경, 외형 색이 변함
        zombieRenderer.material.color = zombieData.skinColor;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        ClearAttack();
        lastAttackTime = Time.time - timeBetAttack;
        pathRoutine = StartCoroutine(UpdatePath());
    }

    private void OnDisable()
    {
        if (pathRoutine != null) StopCoroutine(pathRoutine);
        pathRoutine = null;
        ClearAttack();
        StopMovement();
        targetEntity = null;
    }

    private void StopMovement()
    {
        if (navMeshAgent != null && navMeshAgent.enabled && navMeshAgent.isOnNavMesh)
        {
            navMeshAgent.isStopped = true;
            navMeshAgent.velocity = Vector3.zero;
        }
    }

    private void Update() {
        // 추적 대상의 존재 여부에 따라 다른 애니메이션을 재생
        zombieAnimator.SetBool("HasTarget", hasTarget);
    }

    // 주기적으로 추적할 대상의 위치를 찾아 경로를 갱신
    private IEnumerator UpdatePath() { //start 메서드에서 코루틴으로 UpdatePath() 불러짐
        // 살아있는 동안 무한 루프
        while (!dead)
        {
            if (!navMeshAgent.enabled || !navMeshAgent.isOnNavMesh || isAttacking)
            {
                yield return new WaitForSeconds(0.25f);
                continue;
            }
            if (hasTarget) //hasTarget bool 여부 확인
            {
                // 추적 대상 존재 : 경로를 갱신하고 AI 이동을 계속 진행
                navMeshAgent.isStopped = false;
                UpdateApproach();
            }
            else 
            {
                // 추적 대상 없음 : AI 이동 중지
                navMeshAgent.isStopped = true;

                // 20 유닛의 반지름을 가진 가상의 구를 그렸을때, 구와 겹치는 모든 콜라이더를 가져옴
                // 단, whatIsTarget 레이어를 가진 콜라이더만 가져오도록 필터링
                Collider[] colliders =
                    Physics.OverlapSphere(transform.position, 20f, whatIsTarget); //반지름 20 크기의 구를 생성 콜라이더 배열에 넣기

                // 모든 콜라이더들을 순회하면서, 살아있는 LivingEntity 찾기
                for (int i = 0; i < colliders.Length; i++) //20만큼 for 문 반복
                {
                    // 콜라이더로부터 LivingEntity 컴포넌트 가져오기
                    LivingEntity livingEntity = colliders[i].GetComponent<LivingEntity>();

                    // LivingEntity 컴포넌트가 존재하며, 해당 LivingEntity가 살아있다면,
                    if (livingEntity != null && !(livingEntity is Barricade) && !livingEntity.dead)
                    {
                        // 추적 대상을 해당 LivingEntity로 설정
                        targetEntity = livingEntity;

                        // for문 루프 즉시 정지
                        break;
                    }
                }
            }

            // 0.25초 주기로 처리 반복
            yield return new WaitForSeconds(0.25f); //0.25초를 멈춤 (다른 동작 하지 말고 이것 부터 처리하라)
        }
    }

    private void UpdateApproach() {
        blockingBarricade = null;
        Vector3 origin = transform.position + Vector3.up * attackHeight;
        Vector3 ray = targetEntity.transform.position + Vector3.up * attackHeight - origin;
        RaycastHit[] hits = Physics.RaycastAll(origin, ray.normalized, ray.magnitude,
            attackBlockingLayers, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit hit in hits) {
            if (hit.transform.IsChildOf(transform) || hit.transform.IsChildOf(targetEntity.transform)) continue;
            if (hit.collider.GetComponentInParent<Zombie>() != null) continue;
            Barricade candidate = hit.collider.GetComponentInParent<Barricade>();
            if (candidate != null && candidate.IsBlocking) blockingBarricade = candidate;
            break;
        }
        if (blockingBarricade == null) {
            navMeshAgent.SetDestination(targetEntity.transform.position);
            return;
        }
        Collider blocker = blockingBarricade.Blocker;
        Vector3 surface = blocker.ClosestPoint(transform.position);
        Vector3 towardZombie = transform.position - surface;
        towardZombie.y = 0f;
        Vector3 approach = surface + towardZombie.normalized * (navMeshAgent.radius + 0.1f);
        if (NavMesh.SamplePosition(approach, out NavMeshHit point, 1f, navMeshAgent.areaMask))
            navMeshAgent.SetDestination(point.position);
        else StopMovement();
        // 기존 공격 트리거에 진입하지 않아도 사거리 안이면 공격한다.
        OnTriggerStay(blocker);
    }

    // 데미지를 입었을때 실행할 처리
    public override void OnDamage(float damage,
        Vector3 hitPoint, Vector3 hitNormal) {
        // 아직 사망하지 않은 경우에만 피격 효과 재생
        if (!dead) //죽지 않은 상태
        {
            // 공격 받은 지점과 방향으로 파티클 효과를 재생
            hitEffect.transform.position = hitPoint; //피격된 위치 저장
            hitEffect.transform.rotation
                = Quaternion.LookRotation(hitNormal); //피격된 방향 값 * 90 저장
            hitEffect.Play(); //이펙트 재생

            // 피격 효과음 재생
            zombieAudioPlayer.PlayOneShot(hitSound);
        }

        // LivingEntity의 OnDamage()를 실행하여 데미지 적용
        base.OnDamage(damage, hitPoint, hitNormal);
    }

    // 사망 처리
    public override void Die() {
        // LivingEntity의 Die()를 실행하여 기본 사망 처리 실행
        if (dead) return;
        ClearAttack();
        if (pathRoutine != null) StopCoroutine(pathRoutine);
        pathRoutine = null;
        base.Die();

        // 다른 AI들을 방해하지 않도록 자신의 모든 콜라이더들을 비활성화
        Collider[] zombieColliders = GetComponents<Collider>(); //콜라이더 배열 생성, 컴포넌트들을 다 집어 넣기
        for (int i = 0; i < zombieColliders.Length; i++) //콜라이더 배열의 길이 만큼 for 문으로
        {
            zombieColliders[i].enabled = false; //순차적으로 하나 씩 비활성화
        }

        // AI 추적을 중지하고 내비메쉬 컴포넌트를 비활성화
        StopMovement();
        navMeshAgent.enabled = false;

        // 사망 애니메이션 재생
        zombieAnimator.SetTrigger("Die");
        // 사망 효과음 재생
        zombieAudioPlayer.PlayOneShot(deathSound);
    }

    private void OnTriggerStay(Collider other)
    {
        if (!isActiveAndEnabled || dead || isAttacking || !hasTarget ||
            Time.time < lastAttackTime + timeBetAttack ||
            !navMeshAgent.enabled || !navMeshAgent.isOnNavMesh)
            return;

        LivingEntity attackTarget = other.GetComponentInParent<LivingEntity>();
        LivingEntity expectedTarget = blockingBarricade != null && blockingBarricade.IsBlocking
            ? blockingBarricade : targetEntity;
        if (attackTarget == null || attackTarget != expectedTarget || attackTarget.dead) return;
        if ((other.ClosestPoint(transform.position) - transform.position).sqrMagnitude >
            attackReach * attackReach) return;

        currentAttackTarget = attackTarget;
        currentAttackCollider = other;
        isAttacking = true;
        hitApplied = false;
        lastAttackTime = Time.time;
        StopMovement();
        navMeshAgent.updateRotation = false;

        Vector3 direction = attackTarget.transform.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(direction);

        zombieAnimator.SetTrigger(AttackTrigger);
    }

    // Called only by the attack clip's impact event.
    public void OnAttackHit()
    {
        if (!isActiveAndEnabled || dead || !isAttacking || hitApplied) return;
        hitApplied = true;
        if (currentAttackTarget == null || currentAttackTarget.dead ||
            !currentAttackTarget.isActiveAndEnabled || currentAttackCollider == null ||
            !currentAttackCollider.enabled || !currentAttackCollider.gameObject.activeInHierarchy)
            return;

        Vector3 hitPoint = currentAttackCollider.ClosestPoint(transform.position);
        if ((hitPoint - transform.position).sqrMagnitude > attackReach * attackReach) return;

        // Check obstruction at torso height, ignoring this zombie and its target.
        Vector3 origin = transform.position + Vector3.up * attackHeight;
        Vector3 destination = currentAttackCollider.bounds.center;
        Vector3 ray = destination - origin;
        if (ray.sqrMagnitude > 0.0001f)
        {
            foreach (RaycastHit hit in Physics.RaycastAll(origin, ray.normalized,
                ray.magnitude, attackBlockingLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform.IsChildOf(transform) ||
                    hit.transform.IsChildOf(currentAttackTarget.transform)) continue;
                return;
            }
        }

        Vector3 hitNormal = transform.position - currentAttackTarget.transform.position;
        currentAttackTarget.OnDamage(damage, hitPoint, hitNormal);
    }

    public void OnAttackFinished()
    {
        if (!isAttacking) return;
        ClearAttack();
        if (dead || !isActiveAndEnabled) return;
        zombieAnimator.SetBool("HasTarget", hasTarget);
        if (navMeshAgent.enabled && navMeshAgent.isOnNavMesh)
        {
            navMeshAgent.isStopped = !hasTarget;
            if (hasTarget) UpdateApproach();
        }
    }

    private void ClearAttack()
    {
        isAttacking = false;
        hitApplied = false;
        currentAttackTarget = null;
        currentAttackCollider = null;
        if (zombieAnimator != null) zombieAnimator.ResetTrigger(AttackTrigger);
        if (navMeshAgent != null) navMeshAgent.updateRotation = agentUpdatesRotation;
    }
}