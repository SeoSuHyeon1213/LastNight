# 리뷰 보완 구현과 검증 — 2026-10-06

이번 범위는 일시정지, 단일 거점 진입, 소환·대기 군집 위치 검증, 시야·실내 소음, 보관소 피해다. 자동 바리케이드 설치·강화, 이동 거점, 파밍 확률, 신규 특수 좀비, 도전 과제는 기존 체크리스트의 후속 작업으로 남는다.

## 적용 규칙과 테스트값

- 탐지 반경 8m. 벽 등 물리 차단물과 완전 NavMesh 경로를 확인한다. 추적 확인은 0.25초 간격, 시야/거리 상실 3초 후 탐색, 4초 후 역할 기본 상태로 복귀한다. 강제 각성은 기존 추적 규칙을 유지한다.
- 보관소 HP 1000은 밸런스 검증용이다. 플레이어 총알·수류탄 등의 기존 `IDamageable.OnDamage` 호출은 보관소 피해로 계산하지 않는다. 좀비 타격만 `ReceiveZombieDamage`로 전달한다. HP 0이면 패배, 중복 사망은 무시한다.
- 웨이브 전멸 확인은 다음 프레임에 완료를 확정해 같은 프레임의 플레이어/보관소 파괴를 우선한다. 일시정지에서는 준비 시간뿐 아니라 생성 묶음 예산·대기열·웨이브 전환도 보존한다.
- 열린 완전 경로를 우선하고, 없으면 연결된 바리케이드 외부 지점으로 이동해 공격한다. 바리케이드의 carving 장애물은 판자가 있으면 활성화하고, 파괴·비활성화 시 해제한다. carving 반영에는 게임 프레임 지연이 있다.
- 공유 생성 검증은 에디터에 연결된 실내 구역, 캡슐 충돌, 플레이어 거리, 층 보정(최대 0.5m), 거점 접근 경로를 확인한다. 소환은 예고 완료 때 다시 검사한다. 대기 군집도 실제 생성 직전에 검사하며 실패한 몫은 일반 생성으로 돌린다.
- 실내 소음 반경 배율: 이동·조준 이동·재장전·획득·바리케이드 작업 0.7. 총성 30m/배율 1.0은 기존 테스트값을 유지한다. 폭발·바리케이드 파괴 소음은 후속 작업이다.

## 에디터 구성과 연결

모든 구성은 Unity CLI의 에디터 오브젝트·컴포넌트·Inspector 명령으로 적용했다. 게임 스크립트에서 누락된 씬 구성이나 참조를 생성하지 않는다.

| 대상 | 연결·설정 |
|---|---|
| `/World Rules` | `SpawnLocationValidator`. Base Target → `/Base Storage`, Indoor Zones → 자식 `Indoor 1`~`Indoor 13`의 BoxCollider. |
| `Indoor 1`~`Indoor 13` | 현재 씬의 집 5개 바닥 타일 영역에 맞춘 BoxCollider, Is Trigger 활성화. L자 구조·층별로 구분. 벽·바닥 충돌을 대체하지 않음. |
| `/Enemy Registry` | Spawn Validator → `/World Rules`. 소환자와 대기 군집은 레지스트리의 공유 검증을 사용. |
| `/PlayerCharacter` | `PlayerNoiseEmitter.Area Validator` → `/World Rules`. 기존 Noise Profile/Dispatcher 유지. |
| `/Base Storage` | Game Manager → `/GameManager`, 기존 Attack Collider 유지, Maximum Health 1000, Entry Routes → 두 외부 지점의 `BaseEntryRoute`. |
| `/Entry West Outside`, `/Entry West Inside` | 외부 (425.9, 2.907402, 394.411), 내부 (428.2, 2.907402, 394.411). 외부의 `BaseEntryRoute.Inside`에 내부 Transform, Barricade에 서쪽 방어물 연결. |
| `/Entry East Outside`, `/Entry East Inside` | 외부 (432.9, 2.907402, 399.395), 내부 (430.4, 2.907402, 399.395). 같은 방식으로 동쪽 방어물 연결. |
| 기존 바리케이드 10개 | `NavMeshObstacle` Box, 기존 BoxCollider의 Center/Size와 동일, Carve 활성화, Carve Only Stationary 비활성화, Time To Stationary 0. `Barricade.Navigation Obstacle` 참조 연결. |
| 일반/소환 좀비 프리팹 5개 | Detection Radius 8. Sight Blocking Layers는 기본 Physics 레이어(-5), Trigger 무시. Agent Type 0/반경 0.5 유지. |
| Idle Group/Summon Profile | 캡슐 반경 0.4·높이 2.0. 실제 좀비 Collider 반경 0.2·높이 2.0보다 여유 있게 검증. 기존 수량·거리·시간 설정 유지. |
| Noise Profile | 재장전·획득·작업의 Indoor Multiplier 0.7, 기존 이동·조준 이동 0.7 유지. |
| 집 루트 5개 | 건물 전체를 감싸던 중복 BoxCollider만 비활성화. 개별 벽·바닥 MeshCollider 유지. Terrain NavMesh Surface(Type 0)와 기존 NavMesh 데이터 유지, 링크 추가·재베이크 없음. |

맵이나 프리팹 크기가 변경되면 실내 구역·진입 좌표·캡슐·Agent Type/Area Mask를 함께 다시 확인한다. 현재 씬에는 집 루트가 5개 확인됐으며, 추가 집은 구역을 별도로 구성해야 한다. 거점 이동 시 목표 전환은 이번 범위에 포함하지 않는다.

## 실제 검증 증거

검증은 현재 `Assets/Scenes/Game.unity`의 Play Mode에서 수행했다. 일부 경계 타이머·타격 이벤트는 런타임에서 직접 호출했다. 테스트 소스와 JSON 결과는 프로젝트 외부 `outputs/verification`에 저장해 제품 코드에 포함하지 않았다.

| 시나리오 | 결과 파일 |
|---|---|
| 준비 20초 정지, 빅웨이브 대기열 22/생존 1 보존 | [준비](../../verification/pause-spawning-result.json), [생성 정지](../../verification/pause-check-result.json) |
| 일반 웨이브 대기열 4 보존→재개 후 3 | [일반 생성 정지/재개](../../verification/normal-pause-check-result.json) |
| 양쪽 차단→서쪽 선택, 동쪽 열림 우선, 타격으로 판자 손상, 파괴 후 접근 | [차단](../../verification/routes-open-result.json), [열린 길](../../verification/routes-check-open-result.json), [타격](../../verification/routes-hit-result.json), [접근·피해](../../verification/routes-after-result.json) |
| 실내·충돌·25/29m 거리·층 보정 거부, 유효 대기 3마리, 무효 위치 일반 생성 전환 | [생성 위치](../../verification/spawn-validation-result.json) |
| 예고 후 부분 실패 1마리, 전부 실패 시 횟수 유지, 사망 취소 후 기존 1마리·예약 0 | [소환 재검증](../../verification/summon-revalidation-result.json) |
| 야외 발견·벽 차단·8m 밖 거부·탐색/거점 복귀·실내 재장전 반경 3.5m | [시야·소음](../../verification/vision-and-noise-result.json) |
| 플레이어 피해 제외·좀비 피해 20·HP 0·사망 이벤트 1회·최종 처치와 동시 패배 | [패배 우선](../../verification/defeat-priority-result.json) |
| 실제 RestartScene 후 웨이브 1·HP 1000·예약 0·테스트 객체 제거 | [재시작](../../verification/restart-result.json) |

CLI 테스트 동안 백그라운드에서 게임 프레임이 실제 진행되도록 `Application.runInBackground`를 임시 활성화했으며 false로 복원했다. 테스트 수치·오브젝트·구역 이동은 Play Mode에만 적용했고 실제 재시작 및 Play Mode 종료로 제거했다.

Unity 컴파일 오류는 없었다. 초기 NavMeshPath 필드 생성자 예외는 Awake/지연 초기화로 수정했다. 이후 테스트 구간의 콘솔에는 수정 중 CLI 설정/연결 실패 기록만 있었고 새로운 게임 스크립트 런타임 예외는 없었다. 기존 손상된 `Assets/Prefabs/Zombie.meta` 오류는 이번 변경 범위에서 복구하지 않았다.

미검증: 전체 맵 수동 이동·실내 카메라·다층 물리, 장시간 성능/밸런스, 새 거점으로 전환, 보관소 HP/패배 원인 HUD, 모든 공격 애니메이션의 연출. 소환자의 지정 대기 지점 및 전용 소환 애니메이션/음향도 기존 후속 작업으로 남는다.
