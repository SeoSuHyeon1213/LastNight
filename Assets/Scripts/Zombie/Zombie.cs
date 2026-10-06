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
    private bool movementLocked;
    private bool hitApplied;

    [Header("Detection")]
    [SerializeField, Min(0f)] private float detectionRadius = 8f; // 테스트 시작값
    [SerializeField] private LayerMask sightBlockingLayers = Physics.DefaultRaycastLayers;
    [Tooltip("추적 대상이 탐지 반경 밖에 이 시간 동안 머물면 놓친 것으로 보고 탐색으로 전환")]
    [SerializeField, Min(0f)] private float loseTargetSeconds = 3f;
    [Tooltip("추적 대상을 놓친 뒤 제자리 탐색 시간")]
    [SerializeField, Min(0f)] private float chaseSearchSeconds = 4f;

    [Header("Hearing")]
    [SerializeField] private HearingProfile hearingProfile; // 비워 두면 소음을 듣지 않음
    [SerializeField] private bool drawStateGizmo = true;

    public ZombieState State { get; private set; } = ZombieState.Idle;
    public ZombieRole Role { get; private set; } = ZombieRole.Wave;
    protected BaseStorage BaseTarget => baseTarget;
    private BaseStorage baseTarget; // 스포너(또는 소환자)가 생성 시 전달

    // 대상이 없을 때 돌아갈 상태. Wave 역할이라도 보관소 참조가 없으면 정지(Idle)한다.
    private ZombieState DefaultState =>
        Role == ZombieRole.Wave && baseTarget != null && baseTarget.isActiveAndEnabled ? ZombieState.AttackBase : ZombieState.Idle;

    private float targetLostSince = -1f; // 추적 대상이 반경을 벗어난 시각
    private bool canLoseTarget; // 강제 각성 추적은 한 번 반경 안에 들어온 뒤부터 놓침 판정
    private Vector3 lastKnownTargetPosition;
    private float nextVisionCheck;
    private NavMeshPath visionPath;
    // 소음에 반응하는 종류인지. 소환자·특수 좀비는 재정의해 무시한다.
    protected virtual bool ListensToNoise => true;

    private Vector3 noiseTarget; // 오차가 적용되고 NavMesh로 보정된 조사 지점
    private float noisePerceived; // 현재 목표 소음의 체감 크기 (0~1)
    private float reactAt; // Alert → Investigate 전환 시각
    private float stateDeadline; // Investigate·Search 종료 시각
    private float nextHearTime; // 재반응 대기 종료 시각

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
        visionPath = new NavMeshPath();
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
        ResetNoiseState();
        movementLocked = false;
        ClearAttack();
        lastAttackTime = Time.time - timeBetAttack;
        pathRoutine = StartCoroutine(UpdatePath());
    }

    protected virtual void OnDisable()
    {
        if (pathRoutine != null) StopCoroutine(pathRoutine);
        pathRoutine = null;
        ClearAttack();
        StopMovement();
        targetEntity = null;
        ResetNoiseState();
        movementLocked = false;
    }

    // 파생 클래스(소환자 예고 등)가 이동·공격 개시를 잠시 막는다. 진행 중인 공격은 끝까지 진행된다.
    protected void SetMovementLocked(bool locked)
    {
        movementLocked = locked;
        if (locked) StopMovement();
    }

    protected bool IsAttacking => isAttacking;

    // 대기 군집 구성원 여부. 행동은 일반 좀비와 같고(Idle + 20m 탐지 + 소음), 정체 방지 각성 대상 판정에만 쓴다.
    public bool IsIdleGroupMember { get; private set; }

    public void MarkIdleGroupMember()
    {
        IsIdleGroupMember = true;
    }

    // 정체 방지 각성: 탐지 반경과 무관하게 대상을 지정해 Chase로 전환한다.
    public bool ForceChase(LivingEntity target)
    {
        if (dead || !isActiveAndEnabled || target == null || target.dead) return false;
        targetEntity = target;
        EnterChase();
        canLoseTarget = false;
        if (IsAgentReady && !isAttacking && !movementLocked) navMeshAgent.isStopped = false;
        return true;
    }

    // 생성 직후 스포너·소환자가 호출한다. Wave 역할은 보관소 참조가 있어야 거점 공격을 한다.
    public void AssignRole(ZombieRole role, BaseStorage target)
    {
        Role = role;
        baseTarget = target;
        if (!dead && !hasTarget) EnterDefault();
    }
    protected int NavAreaMask => navMeshAgent != null ? navMeshAgent.areaMask : NavMesh.AllAreas;

    private void StopMovement()
    {
        if (navMeshAgent != null && navMeshAgent.enabled && navMeshAgent.isOnNavMesh)
        {
            navMeshAgent.isStopped = true;
            navMeshAgent.velocity = Vector3.zero;
        }
    }

    private void Update() {
        if (Time.timeScale <= 0f || (GameManager.instance != null && GameManager.instance.isGameover)) return;
        if (!dead)
        {
            if (State == ZombieState.Chase)
            {
                if (Time.time >= nextVisionCheck) {
                    nextVisionCheck = Time.time + 0.25f;
                    TickChase();
                }
            }
            else if (hasTarget)
            {
                // 탐지 코루틴이 대상을 찾으면 어느 상태에서든 추적한다
                EnterChase();
                canLoseTarget = true;
            }
            else
            {
                TickNoiseState();
            }
        }

        // 추적·조사 이동·거점 공격 중에는 이동 애니메이션을 쓴다.
        zombieAnimator.SetBool("HasTarget",
            hasTarget || State == ZombieState.Investigate || State == ZombieState.AttackBase);
    }

    // 주기적으로 추적할 대상의 위치를 찾아 경로를 갱신
    private IEnumerator UpdatePath() { //start 메서드에서 코루틴으로 UpdatePath() 불러짐
        // 살아있는 동안 무한 루프
        while (!dead)
        {
            if (!navMeshAgent.enabled || !navMeshAgent.isOnNavMesh)
            {
                yield return new WaitForSeconds(0.25f);
                continue;
            }
            if (isAttacking || movementLocked)
            {
                // 보관소 공격은 쉬지 않고 이어지므로 공격 중에도 플레이어 탐지는 계속한다
                if (!hasTarget && State == ZombieState.AttackBase) DetectTarget();
                yield return new WaitForSeconds(0.25f);
                continue;
            }
            if (hasTarget) //hasTarget bool 여부 확인
            {
                // 추적 대상 존재 : 경로를 갱신하고 AI 이동을 계속 진행. 놓친 동안은 마지막으로 본 위치로 간다.
                navMeshAgent.isStopped = false;
                if (targetLostSince >= 0f) navMeshAgent.SetDestination(lastKnownTargetPosition);
                else UpdateApproach();
            }
            else
            {
                // 추적 대상 없음 : 거점 공격은 보관소로 이동, 조사는 이동 유지, 그 밖에는 정지
                if (State == ZombieState.AttackBase) UpdateBaseApproach();
                else if (State != ZombieState.Investigate) navMeshAgent.isStopped = true;

                DetectTarget();
            }

            // 0.25초 주기로 처리 반복
            yield return new WaitForSeconds(0.25f); //0.25초를 멈춤 (다른 동작 하지 말고 이것 부터 처리하라)
        }
    }

    // detectionRadius(기본 20) 반지름의 가상의 구를 그렸을때, 구와 겹치는 모든 콜라이더를 가져옴
    // 단, whatIsTarget 레이어를 가진 콜라이더만 가져오도록 필터링. 바리케이드와 보관소는 추적 대상이 아니다.
    private void DetectTarget() {
        Collider[] colliders =
            Physics.OverlapSphere(transform.position, detectionRadius, whatIsTarget);

        // 모든 콜라이더들을 순회하면서, 살아있는 LivingEntity 찾기
        for (int i = 0; i < colliders.Length; i++)
        {
            // 콜라이더로부터 LivingEntity 컴포넌트 가져오기
            LivingEntity livingEntity = colliders[i].GetComponentInParent<LivingEntity>();

            // LivingEntity 컴포넌트가 존재하며, 해당 LivingEntity가 살아있다면,
            if (livingEntity != null && !(livingEntity is Zombie) && !(livingEntity is Barricade) &&
                !(livingEntity is BaseStorage) && !livingEntity.dead && CanSee(livingEntity))
            {
                // 추적 대상을 해당 LivingEntity로 설정
                targetEntity = livingEntity;
                lastKnownTargetPosition = livingEntity.transform.position;

                // for문 루프 즉시 정지
                break;
            }
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

    // 보관소 콜라이더의 가장 가까운 지점 앞으로 이동하고, 사거리 안이면 기존 공격 규칙으로 공격한다.
    private void UpdateBaseApproach()
    {
        blockingBarricade = null;
        if (baseTarget == null || baseTarget.dead || !baseTarget.isActiveAndEnabled || baseTarget.AttackCollider == null)
        {
            EnterIdle();
            return;
        }
        if (baseTarget.TryGetApproach(transform.position, navMeshAgent.agentTypeID, navMeshAgent.areaMask,
            navMeshAgent.radius, out Vector3 destination, out Barricade blocker))
        {
            blockingBarricade = blocker;
            navMeshAgent.isStopped = false;
            navMeshAgent.SetDestination(destination);
            OnTriggerStay(blocker != null ? blocker.Blocker : baseTarget.AttackCollider);
        }
        else
        {
            StopMovement();
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
        if (dead) return;
        ClearAttack();
        if (pathRoutine != null) StopCoroutine(pathRoutine);
        pathRoutine = null;
        ResetNoiseState();
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
        if (!isActiveAndEnabled || dead || isAttacking || movementLocked || Time.timeScale <= 0f ||
            (GameManager.instance != null && GameManager.instance.isGameover) ||
            Time.time < lastAttackTime + timeBetAttack ||
            !navMeshAgent.enabled || !navMeshAgent.isOnNavMesh)
            return;

        LivingEntity expectedTarget = CurrentAttackGoal();
        if (expectedTarget == null) return;
        LivingEntity attackTarget = other.GetComponentInParent<LivingEntity>();
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

    // 지금 공격해도 되는 대상: 추적 중이면 앞을 막는 바리케이드 또는 추적 대상, 거점 공격 중이면 보관소
    private LivingEntity CurrentAttackGoal()
    {
        if ((hasTarget || State == ZombieState.AttackBase) && blockingBarricade != null && blockingBarricade.IsBlocking)
            return blockingBarricade;
        if (hasTarget)
            return blockingBarricade != null && blockingBarricade.IsBlocking ? blockingBarricade : targetEntity;
        if (State == ZombieState.AttackBase && baseTarget != null && baseTarget.isActiveAndEnabled) return baseTarget;
        return null;
    }

    // Called only by the attack clip's impact event.
    public void OnAttackHit()
    {
        if (!isActiveAndEnabled || dead || !isAttacking || hitApplied || Time.timeScale <= 0f ||
            (GameManager.instance != null && GameManager.instance.isGameover)) return;
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
        if (currentAttackTarget is BaseStorage storage) storage.ReceiveZombieDamage(this, damage, hitPoint, hitNormal);
        else currentAttackTarget.OnDamage(damage, hitPoint, hitNormal);
    }

    public void OnAttackFinished()
    {
        if (!isAttacking) return;
        ClearAttack();
        if (dead || !isActiveAndEnabled || movementLocked) return;
        if (navMeshAgent.enabled && navMeshAgent.isOnNavMesh)
        {
            if (hasTarget)
            {
                navMeshAgent.isStopped = false;
                UpdateApproach();
            }
            else if (State == ZombieState.AttackBase)
            {
                UpdateBaseApproach();
            }
            else
            {
                navMeshAgent.isStopped = State != ZombieState.Investigate;
            }
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

    // ---- 소음 청취 (NoiseDispatcher가 호출) ----

    // 청취 판정: 거리 ≤ 반경 × 상태별 감도, 재반응 대기 경과, 반응 중이면 체감 크기가 현재보다 일정 비율 이상 클 것.
    public bool CanHear(in NoiseEvent noise, float distance, out float perceived)
    {
        perceived = 0f;
        if (!ListensToNoise || hearingProfile == null || dead || !isActiveAndEnabled ||
            hasTarget || noise.Radius <= 0f || Time.time < nextHearTime)
            return false;

        float sensitivity = hearingProfile.GetSensitivity(State);
        if (sensitivity <= 0f || distance > noise.Radius * sensitivity) return false;

        perceived = 1f - distance / noise.Radius;
        if (IsReactingToNoise && perceived < noisePerceived * (1f + hearingProfile.RetargetMargin))
            return false;
        return true;
    }

    // 반응 요청. 목표 지점을 NavMesh로 보정하지 못하면 무시하고 false를 반환한다.
    public bool HearNoise(in NoiseEvent noise, float distance, float perceived)
    {
        if (!CanHear(noise, distance, out _) || !IsAgentReady) return false;

        // 플레이어의 정확한 위치가 아니라 소음 지점 주변의 오차 위치를 목표로 삼는다.
        float error = Mathf.Min(distance * hearingProfile.TargetErrorRatio, hearingProfile.MaxTargetError);
        Vector2 offset = Random.insideUnitCircle * error;
        Vector3 guess = noise.Position + new Vector3(offset.x, 0f, offset.y);
        if (!NavMesh.SamplePosition(guess, out NavMeshHit hit, hearingProfile.NavMeshSnapDistance, navMeshAgent.areaMask))
            return false;

        noiseTarget = hit.position;
        noisePerceived = perceived;
        nextHearTime = Time.time + hearingProfile.ReactCooldown;

        if (State == ZombieState.Investigate || State == ZombieState.Search)
        {
            // 이미 이동 중인 좀비는 반응 지연 없이 새 목표로 바꾼다.
            BeginInvestigate();
        }
        else if (State != ZombieState.Alert)
        {
            if (!isAttacking && !movementLocked) StopMovement();
            State = ZombieState.Alert;
            reactAt = Time.time + Random.Range(hearingProfile.MinReactionDelay, hearingProfile.MaxReactionDelay);
        }
        return true;
    }

    private bool IsReactingToNoise =>
        State == ZombieState.Alert || State == ZombieState.Investigate || State == ZombieState.Search;

    protected bool IsAgentReady => navMeshAgent != null && navMeshAgent.enabled && navMeshAgent.isOnNavMesh;

    // 시간 비교는 Time.time(일시정지 시 정지)을 사용한다.
    private void TickNoiseState()
    {
        switch (State)
        {
            case ZombieState.Alert:
                if (Time.time >= reactAt) BeginInvestigate();
                break;
            case ZombieState.Investigate:
                if (Time.time >= stateDeadline || !IsAgentReady)
                {
                    EnterDefault();
                }
                else if (!navMeshAgent.pathPending &&
                    navMeshAgent.remainingDistance <= Mathf.Max(navMeshAgent.stoppingDistance, hearingProfile.ArriveDistance))
                {
                    EnterSearch(hearingProfile.SearchDuration);
                }
                break;
            case ZombieState.Search:
                if (Time.time >= stateDeadline) EnterDefault();
                break;
        }
    }

    private void BeginInvestigate()
    {
        if (!IsAgentReady || hearingProfile == null)
        {
            EnterDefault();
            return;
        }
        State = ZombieState.Investigate;
        stateDeadline = Time.time + hearingProfile.InvestigateMaxDuration;
        navMeshAgent.isStopped = false;
        navMeshAgent.SetDestination(noiseTarget);
    }

    private void EnterSearch(float duration)
    {
        State = ZombieState.Search;
        stateDeadline = Time.time + duration;
        if (IsAgentReady) navMeshAgent.isStopped = true;
    }

    // 추적 유지 판정. 대상이 죽으면 바로 기본 상태, 탐지 반경 밖에 일정 시간 머물면 놓치고 탐색한다.
    private void TickChase()
    {
        if (!hasTarget)
        {
            targetEntity = null;
            targetLostSince = -1f;
            EnterDefault();
            return;
        }
        Vector3 offset = targetEntity.transform.position - transform.position;
        offset.y = 0f;
        if (offset.sqrMagnitude <= detectionRadius * detectionRadius) canLoseTarget = true;
        if (CanSee(targetEntity))
        {
            canLoseTarget = true;
            targetLostSince = -1f;
            lastKnownTargetPosition = targetEntity.transform.position;
            return;
        }
        if (!canLoseTarget) return;
        if (targetLostSince < 0f)
        {
            targetLostSince = Time.time;
        }
        if (Time.time - targetLostSince >= loseTargetSeconds)
        {
            targetEntity = null;
            targetLostSince = -1f;
            EnterSearch(chaseSearchSeconds);
        }
    }

    // 역할에 따른 기본 상태로 돌아간다 (Wave: AttackBase, Idle: Idle).
    private void EnterDefault()
    {
        if (DefaultState == ZombieState.AttackBase)
        {
            State = ZombieState.AttackBase;
            noisePerceived = 0f;
            if (IsAgentReady && !isAttacking && !movementLocked) navMeshAgent.isStopped = false;
        }
        else
        {
            EnterIdle();
        }
    }

    private void EnterIdle()
    {
        State = ZombieState.Idle;
        noisePerceived = 0f;
        if (IsAgentReady && !hasTarget) navMeshAgent.isStopped = true;
    }

    private void EnterChase()
    {
        State = ZombieState.Chase;
        noisePerceived = 0f;
        targetLostSince = -1f;
    }

    // 사망·비활성·재활성 시 반응 상태와 타이머를 모두 지운다.
    private void ResetNoiseState()
    {
        nextVisionCheck = 0f;
        State = ZombieState.Idle;
        noisePerceived = 0f;
        reactAt = 0f;
        stateDeadline = 0f;
        nextHearTime = 0f;
        targetLostSince = -1f;
        canLoseTarget = false;
    }

    // Play 중 머리 위 구 색으로 상태를 표시하고, 반응 중이면 조사 지점까지 선을 그린다.
    private bool CanSee(LivingEntity target) {
        if (target == null || target.dead || !target.isActiveAndEnabled || !IsAgentReady) return false;
        Vector3 delta = target.transform.position - transform.position;
        delta.y = 0f;
        if (delta.sqrMagnitude > detectionRadius * detectionRadius) return false;
        Vector3 origin = transform.position + Vector3.up * attackHeight;
        Vector3 ray = target.transform.position + Vector3.up * attackHeight - origin;
        foreach (RaycastHit hit in Physics.RaycastAll(origin, ray.normalized, ray.magnitude,
            sightBlockingLayers, QueryTriggerInteraction.Ignore)) {
            if (hit.transform.IsChildOf(transform) || hit.transform.IsChildOf(target.transform) ||
                hit.collider.GetComponentInParent<Zombie>() != null) continue;
            return false;
        }
        var filter = new NavMeshQueryFilter { agentTypeID = navMeshAgent.agentTypeID, areaMask = navMeshAgent.areaMask };
        if (!NavMesh.SamplePosition(target.transform.position, out NavMeshHit goal, 1f, filter)) return false;
        return NavMesh.CalculatePath(transform.position, goal.position, filter, visionPath) &&
            visionPath.status == NavMeshPathStatus.PathComplete;
    }

    private void OnDrawGizmos()
    {
        if (!drawStateGizmo || !Application.isPlaying || dead) return;
        Vector3 head = transform.position + Vector3.up * 2.2f;
        Gizmos.color = GetStateColor(State);
        Gizmos.DrawSphere(head, 0.2f);
        if (IsIdleGroupMember) Gizmos.DrawWireCube(head, Vector3.one * 0.6f); // 대기 군집 구성원 표시
        if (IsReactingToNoise)
        {
            Gizmos.DrawLine(head, noiseTarget);
            Gizmos.DrawWireSphere(noiseTarget, 0.4f);
        }
    }

    protected virtual void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 1f, 1f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
#if UNITY_EDITOR
        if (Application.isPlaying)
            UnityEditor.Handles.Label(transform.position + Vector3.up * 2.6f, $"{Role}: {State}");
#endif
    }

    private static Color GetStateColor(ZombieState state)
    {
        switch (state)
        {
            case ZombieState.Alert: return Color.yellow;
            case ZombieState.Investigate: return new Color(1f, 0.5f, 0f);
            case ZombieState.Search: return Color.cyan;
            case ZombieState.Chase: return Color.red;
            case ZombieState.AttackBase: return Color.magenta;
            default: return Color.gray;
        }
    }
}
