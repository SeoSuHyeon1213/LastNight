using System.Collections;
using UnityEngine;
using UnityEngine.AI; // AI, 내비게이션 시스템 관련 코드를 가져오기

// 좀비 AI 구현 //부모 클래스가 monobehavier 클래스를 상속 받기 때문에 단일 상속 상태 임에도 컴포넌트 기능 사용 가능
public class Zombie : LivingEntity {
    public LayerMask whatIsTarget; // 추적 대상 레이어

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

    private float attackReach = 1.5f; //리치

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
        zombieAudioPlayer = GetComponent<AudioSource>();

        // 렌더러 컴포넌트는 자식 게임 오브젝트에게 있으므로
        // GetComponentInChildren() 메서드를 사용
        zombieRenderer = GetComponentInChildren<Renderer>();
    }

    // 좀비 AI의 초기 스펙을 결정하는 셋업 메서드
    public void Setup(ZombieData zombieData) {
        // 체력 설정
        startingHealth = zombieData.health;
        health = zombieData.damage;
        // 공격력 설정
        damage = zombieData.damage;
        // 내비메시 에이전트의 이동 속도 설정
        navMeshAgent.speed = zombieData.speed;
        // 렌더러가 사용중인 머테리얼의 컬러를 변경, 외형 색이 변함
        zombieRenderer.material.color = zombieData.skinColor;
    }

    private void Start() {
        // 게임 오브젝트 활성화와 동시에 AI의 추적 루틴 시작
        StartCoroutine(UpdatePath()); //코루틴으로 사용하면 update와 별개로 작동하여 다른 메서드가 막히지 않게 함
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
            if (hasTarget) //hasTarget bool 여부 확인
            {
                // 추적 대상 존재 : 경로를 갱신하고 AI 이동을 계속 진행
                navMeshAgent.isStopped = false;
                navMeshAgent.SetDestination(
                    targetEntity.transform.position);
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
                    if (livingEntity != null && !livingEntity.dead)
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
        base.Die();

        // 다른 AI들을 방해하지 않도록 자신의 모든 콜라이더들을 비활성화
        Collider[] zombieColliders = GetComponents<Collider>(); //콜라이더 배열 생성, 컴포넌트들을 다 집어 넣기
        for (int i = 0; i < zombieColliders.Length; i++) //콜라이더 배열의 길이 만큼 for 문으로
        {
            zombieColliders[i].enabled = false; //순차적으로 하나 씩 비활성화
        }

        // AI 추적을 중지하고 내비메쉬 컴포넌트를 비활성화
        navMeshAgent.isStopped = true;
        navMeshAgent.enabled = false;

        // 사망 애니메이션 재생
        zombieAnimator.SetTrigger("Die");
        // 사망 효과음 재생
        zombieAudioPlayer.PlayOneShot(deathSound);
    }

    private void OnTriggerStay(Collider other) {

        if (dead || isAttacking)
        return;

    if (Time.time < lastAttackTime + timeBetAttack)
        return;

    LivingEntity attackTarget = other.GetComponent<LivingEntity>();

    if (attackTarget == null ||
        attackTarget.dead ||
        attackTarget != targetEntity)
    {
        return;
    }

    // 이번 공격의 대상을 저장
    currentAttackTarget = attackTarget;
    currentAttackCollider = other;

    isAttacking = true;
    hitApplied = false;
    lastAttackTime = Time.time;

    // 공격 시작 요청. 피해는 Animation Event에서 적용
    zombieAnimator.SetTrigger("HasAttack");

        // 자신이 사망하지 않았으며,
        // 최근 공격 시점에서 timeBetAttack 이상 시간이 지났다면 공격 가능
        // if (!dead && Time.time >= lastAttackTime + timeBetAttack) //마지막 공격 시간에서 미리 정한 0.5초 이상 지남 판정
        // {
        //     // 상대방으로부터 LivingEntity 타입을 가져오기 시도
        //     LivingEntity attackTarget
        //         = other.GetComponent<LivingEntity>(); //LivingEntity 타입 저장

        //     // 상대방의 LivingEntity가 자신의 추적 대상이라면 공격 실행
        //     if (attackTarget != null && attackTarget == targetEntity) //타겟이 정해져(null이 아님) 있고 타켓엔티티 받아 왔는지 확인
        //     {
        //         // 최근 공격 시간을 갱신
        //         lastAttackTime = Time.time;

        //         // 상대방의 피격 위치와 피격 방향을 근삿값으로 계산
        //         Vector3 hitPoint
        //             = other.ClosestPoint(transform.position); //좀비와 플레이어가 가장 가까운 위치를 포인트로 저장
        //         Vector3 hitNormal
        //             = transform.position - other.transform.position; //플레이어의 위치에서 적의 위치를 뺌(other.는 좀비 클래스외 다른 클래스)
        //         //이펙트 실행을 위해 거리 값 지정
        //         // 공격 실행
        //         zombieAnimator.SetTrigger("HasAttack"); //애니메이션 트리거 실행
                
        //         attackTarget.OnDamage(damage, hitPoint, hitNormal);
                
        //     }
        // }
        // if(zombieAnimator.GetBool("HasAttack") || !zombieAnimator.GetBool("HasTarget")) //공격 애니메이션이 끝나면 탈출 애니메이션 실행
        // {
        //     zombieAnimator.SetBool("HasTarget", true); //탈출
        // }
    }

    // Animation Event 연결 확인용 예시
    public void OnAttackHit(Collider other)
    {
        if (dead || !isAttacking || hitApplied)
        return;

        // 빗나간 경우에도 이번 공격의 타격 판정은 한 번으로 끝냄
        hitApplied = true;

        if (currentAttackTarget == null ||
            currentAttackTarget.dead ||
            currentAttackCollider == null)
        {
            return;
        }

        if (!currentAttackCollider.enabled ||
            !currentAttackCollider.gameObject.activeInHierarchy)
        {
            return;
        }

        Vector3 hitPoint =
            currentAttackCollider.ClosestPoint(transform.position);

        // 타격 순간에 대상이 멀어졌으면 빗나감
        float distanceSquared =
            (hitPoint - transform.position).sqrMagnitude;

        if (distanceSquared > attackReach * attackReach)
            return;

        Vector3 hitNormal =
            transform.position - currentAttackTarget.transform.position;

        // PlayerHealth.OnDamage()를 통해 체력·피격음·UI 처리
        currentAttackTarget.OnDamage(damage, hitPoint, hitNormal);
    }
    public void OnAttackFinished()
    {
        isAttacking = false;
        hitApplied = false;

        currentAttackTarget = null;
        currentAttackCollider = null;

        zombieAnimator.ResetTrigger("HasAttack");
    }
}
