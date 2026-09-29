# 바리케이드 초기 구현

## 적용
1. Unity의 스크립트 컴파일 완료 후 Game 씬을 연다.
2. PlayerHouse 아래 Wall_01_Exterior_Doorway_01이 있는지 확인한다.
3. Tools > Zombie > Setup Game PlayerHouse Barricade를 실행한다.
4. 배치된 Barricade Site의 BoxCollider와 5개 Plank Socket을 문 틈에 맞춘 뒤 씬을 저장한다.
   기본 폭/높이는 2m이며 문틀 전체 렌더러 경계의 중심/바닥을 초기 위치로 쓴다. 비대칭 문틀은 수동 조정한다.
5. 기본 나무는 임시 갈색 큐브다. Barricade의 Plank Prefab을 원하는 나무 모델 프리팹으로 바꿀 수 있다.

## 동작과 초기값
- E 키 한 번으로 한 조각 작업 시작(키 유지 불필요), 설치 거리 2.5m, 한 조각당 1.5초 작업/재료 1개 소모.
- 5개 소켓, 한 조각 20 HP, 초기 설치 0개. 전부 설치하면 100 HP.
- 유효 타격마다 최소 한 조각 제거. 피해가 20을 넘으면 ceil(피해/20)개를 제거한다.
- 남은 조각 수 x 20이 체력이다. 따라서 피해 1~20 모두 한 조각/20 HP 감소다.
- 시선/거리/차단 조건이 바뀌거나 사망하면 작업 취소. 피격 시 작업 진행 초기화.
- 작업 완료 전에 재료를 소비하지 않는다. 재료 보유 상한 30, 픽업당 3개.
- 재료는 기존 ItemSpawner 유효 아이템에 한 항목으로 포함되어 동일 확률로 생성된다. 기존 5초 소멸을 따른다.
- Assets/Resources/Barricade에 Plank, Plank Pickup, Barricade Site를 에디터 도구가 생성한다.
- 플레이어의 PlayerHealth가 PlankInventory/BarricadeBuilder를 자동 연결한다.
- 좀비는 0.25초 간격으로 플레이어 방향의 몸통 높이 직선을 검사한다. 첫 장애물이 바리케이드면 우선 접근/공격한다.
- 기존 공격 애니메이션의 OnAttackHit/OnAttackFinished 이벤트를 사용한다.
- 파괴 시 충돌이 해제되고 플레이어 추적을 재개한다. 모든 적과 우회 경로를 고려하는 경로 기반 판단은 이번 범위에 포함하지 않는다.

## 배치 조건
- 문 통로에 걸을 수 있는 NavMesh가 있어야 한다. 바리케이드는 런타임에 BoxCollider로 막는다.
- 생성된 바리케이드의 Default 레이어가 좀비 Attack Blocking Layers에 포함되어야 한다.
- 문틀 자체의 콜라이더가 통로까지 막고 있으면 수정해야 한다.
- 장착 후 스크립트에서 콜라이더를 켜고 끄므로 바리케이드를 고정 장애물로 NavMesh에 굽지 않는다.

## 검증 상태
- Unity 참조 DLL 기반 런타임 소스 전체 C# 컴파일 통과.
- BarricadeSetup 에디터 코드 C# 컴파일 통과.
- Unity 프리팹 생성 메뉴 실행, 씬 저장, Play Mode 설치/취소/파괴/경로 검증 미실행.
- 저장된 Game 씬에서 PlayerHouse 이름을 찾지 못했으므로 현재 열린 Unity 씬에서 배치 메뉴 실행 필요.
- 다음 작업 체크리스트의 은신처 완료 표시는 하지 않았다.
- 단계별 보강 해금/웨이브별 비용 증가는 별도 후속 기능이며 이번 네 가지 요청에는 추가하지 않았다.