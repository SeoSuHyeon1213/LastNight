# CrowZombie 추상 클래스 적용 (2026-10-11)

`CrowZombie : SpecialZombie, IEnemyRegistryClient`로 변경했다. ISpecialZombie와 ISpecialAbility는 부모를 통해 구현한다. 기존 스크립트 메타 GUID와 직렬화 필드를 유지해 프리팹 연결을 보존한다.

## 유지한 특성

- SummonProfile 기반 초기 지연·예고·재사용·재시도 간격과 성공 횟수 제한. 기존 에셋은 4초/2초/12초/3초, 한 번에 2마리, 성공 3회, 귀속 생존 4마리이며 변경하지 않았다.
- 예고 시작 전에 위치와 슬롯을 확보하고, 실제 생성 시 위치를 재검사한다.
- NavMesh, 캡슐 충돌, 플레이어 안전거리, 소환 위치 간격, 실외 구역, 거점까지 경로 검사.
- 전체/귀속 상한과 웨이브 대기열의 우선권.
- 예고 동안 이동·일반 공격 정지. 대기 중 기존 좀비 이동·공격, 소음 무시.
- 예고 표시·예고/소환 효과음·선택 Aura와 실제 생성물의 Wave 역할 및 거점 대상.
- 일부 생성 성공은 성공 1회로 집계하고, 전부 실패하면 횟수를 소비하지 않고 재시도한다.
- 소환자 사망 뒤 이미 소환한 적은 유지한다.
- 기존 SpecialAttack 호출은 대기 중 다음 시도를 앞당긴다. 새 TryUseAbility 호출은 지연/재사용 조건을 검사하고 즉시 시작 가능 여부를 반환한다.

## 공통 클래스 사용

TryPrepareAbility로 위치와 예약을 확보한 후에만 Active 상태가 된다. OnAbilityStarted에서 예고를 시작하고 TickAbility에서 완료 시점을 처리한다. 예고 타이머의 기준은 기존 Time.time을 유지하며 완료 검사는 공통 LateUpdate에서 수행한다(기존 FixedUpdate 대신). 자동 소환 시도와 초기 지연은 FixedUpdate에 남겼다.

SuccessfulSummons는 공통 SuccessfulAbilityUses의 별칭이다. 첫 실제 생성 성공에서 RecordAbilityExecution을 호출하여 배치당 AbilityExecuted 이벤트를 한 번 통지한다. 예고/취소/전부 실패는 성공이 아니다. 배치 완료 시 CompleteAbility로 이동 잠금을 해제한다.

사망·비활성·게임오버 시 공통 취소 훅에서 예약과 예고 표시를 정리한다. 재활성화 시 성공 횟수와 소환 상태를 초기화한다. EnemyRegistry를 교체할 때 이전 활성 예약을 먼저 반환한다. 준비/시작 예외에서도 예약·이동 잠금이 남지 않도록 정리한다.

## 에디터 연결

기존 Assets/Prefabs/Zombie/CrowZombie.prefab의 CrowZombie 컴포넌트를 그대로 사용한다. SpecialZombie는 추상 클래스이므로 추가하지 않는다. 일반 Zombie 컴포넌트도 함께 추가하지 않는다.

기존 Zombie Datas 3개, Zombie Prefab 2개, Summon Profile, Summon Telegraph, Spawn Audio/Sound, 피격/사망 참조가 유지됨을 확인했다. Summoner Aura와 Telegraph Sound는 선택 사항이며 기존처럼 비어 있다. 추가로 연결해야 하는 필수 필드는 없다. EnemyRegistry/SpawnLocationValidator와 기존 NavMeshAgent·Animator·AudioSource 구성도 유지한다.

## 검증

Unity 6000.3.10f1 컴파일 통과. 실제 Game 씬의 NavMesh에 기존 CrowZombie 프리팹과 별도 테스트 레지스트리를 런타임 생성해 31개 검증을 통과했다. 테스트 설정은 SummonProfile 복사본에서 조정했으며 원본 에셋을 수정하지 않았다. 임시 객체와 Random/게임오버/시간 상태를 정리하고 Play Mode를 종료했다.

상속·인터페이스, 예고 예약/이동 잠금, 중복 시작/완료 방지, 일시정지, 정상 생성과 성공 이벤트 1회, 재사용 간격, 위치 전부/일부 실패, 전체/귀속 상한, 게임오버/비활성 취소, 재활성 초기화/초기 지연, 성공 횟수 소진, 기존 SpecialAttack 호출, 예고 중 사망/표시 제거와 생성물 생존을 확인했다.

예고 종료 시각을 테스트에서 앞당기고 실제 생명주기 메서드를 호출해 경계 조건을 검사했다. 전체 2초 예고/12초 재사용을 자연 시간으로 관찰하거나 두 소환자의 동시 실행·능력 이벤트 구독자의 재진입·효과음 청취를 이번 검증에서 수행하지 않았다.