# 탱킹형 좀비 설정

## 작성된 동작

TankZombie는 SpecialZombie를 상속하고 기존 이동·탐지·일반 근접 공격·사망 집계를 유지한다. 상시 장갑과 위치 기반 노출 약점을 구현했다. Armor Damage Multiplier 기본 0.5는 원래 피해의 50%, Weak Point Damage Multiplier 기본 1은 약점에 원래 피해 100%를 적용한다. 수치는 Inspector에서 조정하는 테스트 시작값이다. 최종 피해는 정수화하며 HP를 초과해 차감하지 않는다. 죽었거나 비활성인 개체, 음수·0·NaN·무한 피해는 무시한다.

장갑은 실행형 능력이 아니므로 TryUseAbility는 false이며 SuccessfulAbilityUses와 AbilityExecuted를 갱신하지 않는다. 내려찍기·보호 오라·돌진·장갑 파괴 같은 추가 능력은 구현하지 않았다.

## Unity Editor 연결 (아직 미수행)

1. Assets/Prefabs/Zombie/SpecialZombie/TankZombie.prefab 루트에 TankZombie를 추가한다. 일반 Zombie/CrowZombie/BombZombie와 중복 부착하지 않는다.
2. 기존 Zombie와 같은 NavMeshAgent·Animator·AudioSource·몸 Collider·자식 Renderer와 Hit Effect/Hit Sound/Death Sound/What Is Target, 공격 타격/종료 애니메이션 이벤트를 구성한다.
3. 전용 ZombieData를 만들고 해당 프리팹과 함께 스포너에 연결한다. 높은 HP와 느린 속도는 이 데이터에서 정한다. 조정 시작 예시: HP 300, Speed 1.2. 공유 일반 좀비 데이터를 변경하지 않는다.
4. 머리나 등 노출 부위에 자식 Transform(예: BackWeakPoint)을 만들고 Weak Points 배열의 Center에 연결한다. 움직이는 부위라면 해당 뼈의 자식으로 배치한다. Radius 기본 0.25는 **월드 단위**이며 모델 스케일로 자동 확대되지 않는다.
5. 약점은 Collider가 아니라 실제 타격 위치가 Center 반경 안인지로 판정한다. 별도 Trigger나 피격 전달 스크립트는 필요 없다. 중심과 반경이 실제 몸 Collider의 **표면**을 포함하도록 Scene의 노란색 Gizmo를 보고 조정한다. 기존 몸 Collider가 모델 뒤쪽까지 정확히 맞는지 확인한다. 시각적인 노출 부위 표시도 모델/재질에서 구성한다.
6. 현재 일반 Gun은 맞은 Collider에서 직접 IDamageable을 찾으므로 몸 Collider를 TankZombie와 같은 루트에 둔다. 약점용 자식 Collider만 별도로 추가하면 일반 총기가 부모를 찾지 못한다. 스크립트는 자동으로 Collider나 참조를 생성하지 않는다.
7. 유효 약점은 이 개체의 활성 자식 Center와 양수 Radius가 필요하다. 유효 약점이 하나도 없으면 정상 피해를 적용하고 활성화 시 오류를 남긴다. 최소 한 약점이 정상 연결됐는지 확인한다.

## 기존 피해 시스템과의 범위

Gun·Sniper·Grenade·BombZombie가 전달하는 hitPoint에 같은 방어 판정이 적용된다. 폭발도 전달된 가장 가까운 피격 위치가 약점 영역에 들어가면 약점 피해를 받는다. 별도 폭발 관통이나 피해 종류별 방어 규칙은 추가하지 않았다.

기존 Shotgun은 대상별 산탄 피해를 합산한 후 대표 타격 위치 하나를 전달하므로 합산 피해 전체가 그 위치의 장갑/약점 비율을 따른다. 산탄 한 발씩 장갑/약점을 구분하려면 후속 무기 피해 API 확장이 필요하다. 실제 전투에서 이 동작을 확인한다.

IsWeakPointHit(Vector3)는 후속 급소 명중 시스템에서 사용할 수 있지만 도전 집계를 이번 작업에서 추가하지 않았다.

## 검증 증거

Unity 6000.3.10f1 컴파일 오류 없음. 실제 Crow 프리팹의 임시 복사본을 TankZombie로 교체하고 Play Mode에서 17개 검사 통과: 상속·인터페이스, 능동 능력 없음, 장갑 피해 감소, 물리 Raycast가 루트 몸 Collider를 타격해 기존 Gun의 IDamageable 조회와 약점 판정 가능, 약점 정상 피해, 중복 약점 영역 단일 피해, 약점 배율 조정, 상시 특성의 이벤트/횟수 미집계, 잘못된 피해 무시, 0 법선 안전 처리, 약점 비활성 시 정상 피해, 이동하는 약점 중심 반영, 비활성 피해 무시, 재활성 초기화, 치명적 피해·0 HP 사망 통지, 중복 사망 방지.

테스트 오브젝트를 제거하고 Play Mode를 종료했다. 저장된 씬·프리팹은 수정하지 않았다. 실제 TankZombie 프리팹의 HP·속도·큰 체구에 맞는 Agent/Collider·모델과 약점 표시·무기 발사·폭발·공격/음원 연출은 미검증이며 Editor 연결 후 확인해야 한다.
