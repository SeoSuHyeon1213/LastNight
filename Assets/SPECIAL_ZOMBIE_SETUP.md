# 특수 좀비 기본 구조

## 상속과 역할

- `Zombie`: 기존 탐지·추적·일반 공격·체력·사망·웨이브 등록.
- `ISpecialZombie`: 기존 `SpecialAttack()` 호출 계약. `CrowZombie`의 기존 호출과 호환한다.
- `ISpecialAbility`: 능력 상태·시작 가능 여부·성공 실행 횟수·성공 실행 이벤트·시작/취소 계약.
- `SpecialZombie : Zombie, ISpecialZombie, ISpecialAbility`: 새 특수 좀비의 공통 추상 클래스.
- `SpecialAbilityState`: `Ready`(대기), `Active`(예고/실행/회복 진행), `Dead`(사망).

`BombZombie`는 `SpecialZombie`를 상속해 사망 폭발을 구현했다. TankZombie는 상시 장갑·약점을 구현했다. DealerZombie는 플레이어 대상 예고·강타·회복을 구현했다. `CrowZombie`는 `SpecialZombie`를 상속하며 기존 소환 특성을 유지한다. 기존 스포너가 사용하는 `Zombie[]`, 적 등록·사망 집계와의 타입 호환성을 유지한다.

## 파생 클래스가 구현할 부분

| 확장 지점 | 책임 |
|---|---|
| `CanBeginAbility` | 목표 유효성, 거리, 재사용 대기시간 및 종류별 조건 |
| `TryPrepareAbility()` | 위치·예약 등 준비 확보. false면 활성화하지 않음(기본 true) |
| `OnAbilityStarted()` | 예고와 상태 초기화. 공통 클래스가 이미 Active로 전환함 |
| `TickAbility(float deltaTime)` | 예고·실행·회복 타이머 진행. 일시정지 때 호출되지 않음 |
| `OnAbilityCancelled()` | 예약·예고 효과·진행 중 작업을 정리. 취소는 성공 실행이 아님 |
| `OnAbilityReset()` | 재활성 시 종류별 런타임 값 초기화(선택) |

실제 폭발·소환·타격이 성공했을 때 `RecordAbilityExecution()`을 호출한다. 예고 시작만으로 호출하지 않는다. 한 번의 활성화에서 중복 호출은 무시하며 `SuccessfulAbilityUses`와 `AbilityExecuted`를 한 번 갱신한다. 효과 자체의 중복 실행 방지는 파생 클래스가 이 반환값 또는 자체 상태로 보장해야 한다. 능력 처리를 끝낼 때 `CompleteAbility()`를 호출한다. 재사용 간격은 `CanBeginAbility`에서 관리하며 공통 클래스가 임의의 수치를 강제하지 않는다.

사망·비활성·게임오버는 진행 중 능력을 취소한다. 재활성화는 공통 상태와 성공 횟수를 초기화한다. 기본적으로 능력 진행 동안 기존 이동/일반 공격 개시를 잠그며 `Lock Movement During Ability`로 조정한다. 파생 클래스는 기본 생명주기 대신 위 확장 지점을 사용한다. `OnEnable`, `OnDisable`, `Die`, `LateUpdate`를 재정의해야 할 경우 부모 호출을 유지해야 한다.

탱킹형의 상시 장갑·약점은 실행형 능력으로 집계하지 않는다. 실행형 능력이 아직 없다면 `CanBeginAbility`가 false를 반환하도록 구현하고, 능력 사용 전 처치 도전 후보에 포함하지 않는다.

## 프리팹 연결 단계

추상 `SpecialZombie` 자체는 Add Component로 부착할 수 없다. 실제 동작이 완성된 `BombZombie`, `TankZombie`, `DealerZombie` 컴포넌트를 각 프리팹 루트에 붙이는 단계는 후속 작업이다. 같은 오브젝트에 일반 `Zombie` 컴포넌트를 함께 붙이지 않는다.

기존 `Zombie`에 필요한 루트 `NavMeshAgent`, `Animator`, `AudioSource`, 몸 Collider와 자식 Renderer를 구성하고, 추적 대상 Layer·피격 ParticleSystem·피격/사망 AudioClip·공격 애니메이션 이벤트를 Inspector에서 연결한다. 스포너의 프리팹과 `ZombieData`도 연결해야 한다. `ZombieData`는 공유 설정이며 런타임 상태를 저장하지 않는다.

폭발형은 사용자 방향인 작은 체구·빠른 접근을 따른다. 몸 크기와 NavMeshAgent 크기를 맞추고 속도는 기존 `ZombieData.speed`에서 조정한다. 이번 기본 구조 작업에서는 모델·크기·속도·폭발 반경·HP·등장 웨이브를 변경하거나 확정하지 않았다.

## 웨이브 특수 좀비 스폰 설정 (2026-10-13)

Game 씬의 Spawn Points에 있는 ZombieSpawner에서 다음을 Inspector로 연결한다.

1. 기존 Zombie Prefab 배열에는 일반 Zombie만 남긴다. 특수 좀비가 들어 있으면 경고를 출력하고 일반 추첨에서 제외한다.
2. Special Spawn Chance는 백분율이며 기본값 10이다. 0은 일반만, 100은 스폰 지점 생성에 특수만 사용한다. 대기 군집은 항상 일반 좀비다.
3. Special Zombies 배열에 항목을 추가하고 Prefab에는 BombZombie·TankZombie·DealerZombie·CrowZombie 중 사용할 프리팹을, Data에는 그 종류의 ZombieData를 연결한다. 타입별 전용 데이터가 그대로 적용된다.
4. 유효한 특수 항목끼리는 동일 확률로 선택한다. 네 종류라면 기본 설정에서 각각 스폰 지점 생성의 약 2.5%이며, 웨이브마다 정확한 마릿수를 보장하지 않는다.
5. 특수 생성은 기존 웨이브 예정 수의 한 마리를 대체한다. 일반·빅웨이브의 생성 간격, EnemyRegistry의 생존 상한·예약·사망 처리와 거점 목표는 기존 경로를 사용한다.
6. 특수 목록이 비었거나 모든 항목이 잘못 연결되면 경고 후 일반만 생성한다. 일반 Prefab·Data·Spawn Points·Enemy Registry는 기존처럼 필수다.

확률은 실행 중 Inspector에서 조정할 수 있다. 프리팹·데이터 목록은 Awake에서 검증·캐시하므로 목록 변경 후에는 Play Mode를 다시 시작한다. 연결 및 실제 웨이브 스폰 검증은 코드 확인과 별도로 진행한다.

검증: Unity 컴파일 오류 없음. 임시 추첨 검사에서 빈 목록·NaN·음수·0%·100%·프리팹/전용 데이터 짝 유지·종류별 균등 선택을 확인했다. 고정 시드로 10만 번 추첨 시 특수 10,000회(탱커 4,910회, 폭발형 5,090회). 실제 씬 Inspector 연결과 Play Mode 웨이브 생성·사망 집계는 미검증이다.

## CrowZombie 기존 능력 적용

CrowZombie의 소환 타이밍·상한·위치 검사·예약·음원 설정은 유지한다. SuccessfulSummons는 SuccessfulAbilityUses와 같은 값이며 실제 소환 성공에서 AbilityExecuted를 한 번 통지한다. SpecialAttack은 기존처럼 대기 중 다음 물리 업데이트의 시도를 앞당긴다. 소환 예고는 LocksMovementDuringAbility 재정의로 항상 이동/일반 공격을 잠근다. 상세 검증은 CROW_ZOMBIE_MIGRATION.md를 참고한다.

## 확인 범위

Unity 컴파일과 타입 호환성 확인. CrowZombie는 기존 프리팹 참조와 Play Mode 검증 31개를 확인했다. BombZombie는 사망 폭발 스크립트와 임시 Play Mode 검증 12개를 완료했다(BOMB_ZOMBIE_SETUP.md). TankZombie는 장갑·약점 스크립트와 임시 Play Mode 검증 17개를 완료했다(TANK_ZOMBIE_SETUP.md). DealerZombie는 예고·강타 스크립트와 임시 Play Mode 검증 23개를 완료했다(DEALER_ZOMBIE_SETUP.md). 실제 특수 좀비 프리팹 연결은 후속 작업이다. 확인 시 중복 시작·중복 성공 이벤트·예고 중 사망·비활성 후 재활성·일시정지·게임오버와 기존 적 등록/사망 집계를 포함한다.

사망 시 발동하는 능력은 기본 Die 통지 전에 RecordDeathAbilityExecution을 사용할 수 있다. NavMesh·일반 공격 상태로 사망 반응을 제한하지 않으며, 파생 클래스에서 중복 사망과 게임 종료 시 정리를 먼저 방지한다. 일반 실행형 능력은 기존 RecordAbilityExecution을 유지한다.
