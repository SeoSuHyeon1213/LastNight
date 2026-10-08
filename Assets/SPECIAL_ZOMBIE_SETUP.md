# 특수 좀비 기본 구조

## 상속과 역할

- `Zombie`: 기존 탐지·추적·일반 공격·체력·사망·웨이브 등록.
- `ISpecialZombie`: 기존 `SpecialAttack()` 호출 계약. `CrowZombie`의 기존 호출과 호환한다.
- `ISpecialAbility`: 능력 상태·시작 가능 여부·성공 실행 횟수·성공 실행 이벤트·시작/취소 계약.
- `SpecialZombie : Zombie, ISpecialZombie, ISpecialAbility`: 새 특수 좀비의 공통 추상 클래스.
- `SpecialAbilityState`: `Ready`(대기), `Active`(예고/실행/회복 진행), `Dead`(사망).

폭발·탱킹·딜링 종류의 실제 클래스는 다음 단계에서 `SpecialZombie`를 상속한다. 기존 소환자 `CrowZombie`는 이번에 상속 구조를 변경하지 않았다. 기존 스포너가 사용하는 `Zombie[]`, 적 등록·사망 집계와의 타입 호환성을 유지한다.

## 파생 클래스가 구현할 부분

| 확장 지점 | 책임 |
|---|---|
| `CanBeginAbility` | 목표 유효성, 거리, 재사용 대기시간 및 종류별 조건 |
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

## 확인 범위

Unity 컴파일과 타입 호환성 확인. 실제 구체 능력, 프리팹 연결, Play Mode 능력 상태 전환은 후속 구현에서 검증한다. 확인 시 중복 시작·중복 성공 이벤트·예고 중 사망·비활성 후 재활성·일시정지·게임오버와 기존 적 등록/사망 집계를 포함한다.
