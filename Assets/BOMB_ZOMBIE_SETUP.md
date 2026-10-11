# 폭발형 좀비 설정

## 작성된 동작

`BombZombie : SpecialZombie`는 살아 있을 때 기존 Zombie 추적·근접 공격을 사용하며, 사망할 때 즉시 폭발한다. 능동 자폭·접근 기폭 예고는 이번 범위에 포함하지 않았다. 플레이어와 다른 Zombie 계열에 반경 내 고정 피해를 적용한다. 다른 폭발형이 이 피해로 죽으면 연쇄 폭발한다. 벽·지형 차단과 대상별 중복 피해 방지를 적용한다. 바리케이드·보관소 피해는 이번 요청 범위에 포함하지 않았다.

사망 폭발은 일반 공격 중이거나 NavMeshAgent가 경로를 사용할 수 없어도 실행된다. `RecordDeathAbilityExecution()`은 기본 사망 이벤트 전에 능력 실행을 기록한다. 폭발·연쇄 피해가 끝난 뒤 기본 Die를 호출해 적 등록 해제와 점수·사망 통지가 중복되지 않게 한다. 단순 비활성·Destroy는 폭발하지 않는다. 이미 게임오버인 상태에서 Die를 호출한 정리는 피해 없이 기본 사망 처리만 실행한다.

## Unity Editor 연결 (아직 미수행)

1. `Assets/Prefabs/Zombie/SpecialZombie/BombZombie.prefab` 루트에 BombZombie를 부착한다. Zombie/CrowZombie 컴포넌트와 중복 부착하지 않는다.
2. 기존 Zombie와 같이 루트 NavMeshAgent, Animator, AudioSource, 몸의 비 Trigger Collider, 자식 Renderer와 피격 ParticleSystem을 구성한다. Animator의 공격 타격 이벤트와 Hit Effect, Hit Sound, Death Sound, What Is Target을 연결한다. What Is Target은 플레이어 등 추적 대상만 포함하고, 아군 좀비를 추적 대상으로 넣지 않는다.
3. 작은 모델 크기에 CapsuleCollider와 NavMeshAgent Radius/Height/Base Offset을 맞춘다. 크기는 스크립트가 강제로 변경하지 않는다.
4. 폭발형 전용 ZombieData를 만들고 스포너의 해당 프리팹과 데이터에 연결한다. 예시 시작값: Health 30, Speed 4.5. 기존 일반 좀비보다 낮은 HP·빠른 속도를 확인하며 조정한다. 공유 일반 ZombieData를 수정하지 않는다.
5. Explosion Radius 기본 3m, Explosion Damage 기본 50은 테스트 시작값이다. Explosion Offset 기본 (0, 0.5, 0)은 월드 좌표 오프셋이므로 몸 중심에 맞게 조정한다.
6. Damage Layers에 플레이어·좀비 **몸 Collider의 실제 레이어**를 포함한다. 탐지용 Trigger만으로는 폭발 피해를 받지 않는다. Blocking Layers에는 벽·지형·방어 구조물 등 차단물 레이어를 포함한다. 생명체의 몸은 서로의 폭발 피해를 가리지 않는다.
7. Explosion Effect Prefab·Explosion Sound는 선택 연결이다. 이펙트는 본체와 별도로 생성하고 설정한 Lifetime 뒤 제거한다. 실행 중 주황색 선택 Gizmo로 반경을 확인한다.

## 검증 증거와 남은 확인

Unity 6000.3.10f1 컴파일 오류 없음. 기존 Crow 프리팹을 임시 복사해 BombZombie로 교체한 Play Mode 테스트에서 12개 검사 통과: 능동 기폭 없음, 치명적 피해 사망 폭발, NavMesh 미사용 상태 발동, 플레이어 타입 수신기와 여러 Collider의 단일 피해, 능력/사망 이벤트 재진입 및 반복 호출 방지, 벽 차단, 반경 밖 제외, 좀비끼리 연쇄 폭발과 각각 단일 사망, 비활성 무폭발, 재활성 상태 초기화, 게임오버 정리 무피해.

임시 플레이어 수신기는 PlayerHealth 파생 테스트 타입을 사용했다. 실제 PlayerHealth HUD·사망·게임오버 연동, 최종 적 처치와 플레이어 사망 동시 처리, 실제 BombZombie 프리팹의 작은 크기·속도·HP·Animator·음원·이펙트는 미검증이다. 테스트 오브젝트는 제거했고 Play Mode를 종료했다. 저장된 씬/프리팹은 이 작업에서 수정하지 않았다.

## 기획과의 변경점

최신 사용자 요청에 따라 이전 기획의 ‘예고 중 처치하면 기폭 취소·사망 후 피해 폭발 없음’을 ‘처치 시 폭발·다른 좀비도 피해’로 변경했다. 사망 폭발은 처치로 예방할 수 없으므로 ‘능력 사용 전 처치’ 도전에서 폭발형을 어떻게 취급할지는 후속 도전 설계에서 정해야 한다.
