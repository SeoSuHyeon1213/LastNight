# 딜링형 좀비 설정

## 작성된 동작

DealerZombie : SpecialZombie는 탐지할 대상을 PlayerHealth로 제한해 플레이어를 추적한다. 플레이어가 아직 감지되지 않았다면 기존 Wave 역할의 거점 접근을 유지하고, 바리케이드가 플레이어 접근을 막으면 기존 일반 근접 공격으로 처리한다. ForceChase로 외부에서 지정하는 대상 계약은 유지한다.

플레이어를 공격할 때 일반 공격을 대신해 예고 → 전방 강타 → 회복 → 재사용 대기 순서로 동작한다. 예고 시작 시 공격 방향을 고정하고 NavMeshAgent의 회전과 이동을 예고·회복 동안 잠근다. 타격 순간 대상 생존·활성·몸 Collider·실제 거리·고정 방향의 전방 각도·차단물을 재검사한다. 빗나가도 회복과 대기 시간을 유지하며 유효한 타격만 능력 횟수/이벤트로 기록한다. 사망·비활성·게임오버는 진행 중 강타를 취소하고 표시·회전 잠금을 해제한다. 일시정지·키 재설정 중에는 공통 능력 타이머가 진행되지 않는다.

기본 테스트 시작값은 Windup 0.7초, Recovery 1.2초, Cooldown 2초(회복 종료부터), Damage Multiplier 2, Reach 1.8m, Half Angle 45도이다. 피해는 ZombieData.damage × 배율을 최종 정수화한다. 바리케이드/보관소의 일반 공격에는 배율을 적용하지 않는다. 돌진이나 원거리 공격은 추가하지 않았다.

## Unity Editor 연결 (아직 미수행)

1. Assets/Prefabs/Zombie/SpecialZombie/DealerZombie.prefab 루트에 DealerZombie를 붙인다. 일반 Zombie나 다른 특수 좀비와 중복 부착하지 않는다.
2. 기존 Zombie에 필요한 루트 NavMeshAgent·Animator·AudioSource·몸 Collider·공격 범위 Trigger와 자식 Renderer, Hit Effect/Hit Sound/Death Sound를 구성한다. What Is Target에는 실제 플레이어 몸 Collider의 레이어를 포함한다.
3. 전용 ZombieData를 만들고 프리팹과 함께 스포너에 연결한다. HP·속도·기본 Damage는 데이터에서 조정한다. 예시 시작값: HP 100, Speed 3, Damage 20(강타는 기본 배율로 40). 일반 좀비 데이터를 공유 수정하지 않는다.
4. 공격 시작은 기존 Zombie의 공격 범위 Trigger와 Attack Reach 검사로 들어온다. Attack Reach 기본 1.5m와 Strike Reach 기본 1.8m는 별도 검사다. 실제 시작 거리와 타격 거리를 의도에 맞게 조정하고, Agent Stopping Distance 때문에 시작 거리에 진입하지 못하지 않는지 확인한다.
5. Strike Blocking Layers에 벽·지형·바리케이드 등 차단 레이어를 연결한다. 공격자와 목표의 몸은 차단 검사에서 제외된다. 다른 좀비 등 중간 Collider는 포함된 레이어에 따라 강타를 차단한다.
6. Attack Trigger 기본 HasAttack은 Animator의 Trigger 이름이다. 예고 시작에 실행되므로 실제 클립의 준비 자세와 타격 순간을 Windup Seconds에 맞춘다. 딜링형 플레이어 강타의 피해는 타이머로만 적용한다. 기존 OnAttackHit/OnAttackFinished 이벤트는 바리케이드·보관소 일반 공격에 필요하며, 강타에서는 중복 피해를 주지 않는다. Root Motion은 기존 Zombie처럼 비활성으로 유지한다.
7. Windup Indicator는 **기존 자식 오브젝트**를 연결하고 공격 예고를 명확히 보이게 구성한다. 본체나 외부 씬 오브젝트를 연결하지 않는다. Windup Sound는 선택 연결이다. 스크립트가 예고 표시 계층을 자동 생성하지 않는다.
8. 예고·회복 중 HasTarget 이동 애니메이션 Bool을 false로 갱신한다. 실제 Animator의 공격/회복 전환과 공격 예고를 사용자가 보고 피할 수 있는지 확인한다.

## 변경한 기반 코드

Zombie에 CanChaseTarget와 BeginMeleeAttack 두 protected virtual 확장 지점을 추가했다. 기본 구현은 기존 탐지 대상 조건과 일반 공격 시작 처리이며, DealerZombie만 플레이어 대상과 강타 시작을 재정의한다. 기존 공격 타격/종료 이벤트를 유지한다. SpecialZombie는 이번 작업에서 변경하지 않았다.

## 검증 범위

Unity 6000.3.10f1 컴파일 오류 없음. 기존 Crow 프리팹의 임시 복사본을 DealerZombie로 교체하고 실제 Game NavMesh 위에서 상태·물리 경계를 검증했다. 피해 수신기는 PlayerHealth 파생 임시 테스트 타입이며, 타이머 단계를 직접 진행해 결과를 확인했다. 저장된 프리팹/씬은 수정하지 않았고 테스트 오브젝트는 제거했다.

23개 검사: NavMesh 지점/Agent 준비, 기존 Trigger에서 강타 자동 시작, 이동/회전 잠금, 중복 시작·예고 중 무피해, 기존 타격 이벤트 중복 방지, 일시정지, 강타 단일 피해/실행 이벤트, 회복 잠금·중복 공격 방지·종료 복귀, 쿨다운, 옆 회피와 고정 방향·거리 이탈·중간 벽·Collider 비활성의 무피해, 비활성 취소·재활성 초기화, 게임오버 취소, 사망 콜백의 Dead 상태·예고 중 사망 무피해, 기존 일반 근접 공격 피해/중복 방지.

실제 DealerZombie 프리팹 연결, 자연 시간으로 진행되는 애니메이션/음원·예고 가독성, 실제 플레이어 HUD·사망 연동, 여러 좀비와 바리케이드 경로 통합은 미검증이다. 능력 사용 전 처치 도전의 딜링형 대상 정책은 별도 도전 시스템 작업에서 연결한다.
