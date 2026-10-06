# CLAUDE.md — Zombie TPS (LastNight) 구현 작업 지침

이 파일은 `Assets/` 아래에서 Claude Code로 C# 구현 작업을 할 때 적용한다. 코드 규칙의 원본은 `AGENTS.md`이며 여기서 중복해서 쓰지 않는다.

@AGENTS.md

## 1. 프로젝트와 문서 위치

- Unity 프로젝트 루트는 이 폴더의 상위(`LastNight/`)이며 Git 브랜치는 `develop`이다.
- Unity `6000.3.10f1`(`ProjectSettings/ProjectVersion.txt`), URP 17.3.0. 설치된 패키지에 AI Navigation 2.0.13, Input System 1.18.0, Test Framework 1.6.0이 있다.
- 기존 코드는 레거시 `Input` 클래스를 쓴다(`GameSessionManager` 등). 새 입력 코드를 쓰기 전에 Player Settings의 Active Input Handling을 확인한다.
- 씬: `Game`, `Intro`, `Main`, `Practice`. 게임플레이는 `Game` 씬이다.
- Test Framework 패키지는 있으나 프로젝트 자체 테스트 어셈블리(`*.asmdef`)는 없다. 테스트를 추가하려면 먼저 사용자에게 알린다.

## 2. 문서 우선순위

충돌하면 위에서부터 따른다.

1. 사용자의 최신 요청
2. `AGENTS.md` (규칙, 책임 경계, 에디터 연결 원칙)
3. `TPS_좀비_서바이벌_상세기획서_v0.1.md`의 **0장** (현재 코드 기준선). 파일명은 v0.1이지만 본문은 v0.8이다.
4. 같은 기획서의 확장안(2~15장). 구현할 목표이며 현재 동작이 아니다. `제안`·`미정`·`보류` 표기는 확정 사양이 아니다.
5. `NEXT_WORK_CHECKLIST.md` (진행 상태. 증거 없이 체크하지 않는다)

기획서는 85KB이므로 통째로 읽지 않는다. 0장과 이번 작업에 해당하는 장만 읽는다.

**문서 불일치 (사용자 확인 필요)**

- 작업 절차와 기존 무기·HUD·보급 설계의 구현 경계는 `AGENTS.md` 2.2절과 11절에 통합되어 있다. 작업 시작 시 `AGENTS.md`와 `NEXT_WORK_CHECKLIST.md`를 함께 확인한다.
- 기획서 5장의 4.5/2.5 이동 속도 등 v0.7 수치와 6.4절의 "미적용·보류" 설명이 함께 남아 있다. 0장과 7.0절(코드 기반 물량)이 우선이다.

## 3. Claude Code 작업 방식

**실행 환경을 먼저 확인한다.** 사용자가 허용한 Unity CLI로 연결된 Editor가 있으면 에디터 명령과 Play Mode 검증을 사용할 수 있다(6절 참고). 연결이 불가능하면 미검증으로 남긴다. 보고할 때 다음을 분리한다.

- 코드 작성 완료
- 사용자가 에디터에서 연결할 항목 (AGENTS.md 2.1의 절차 형식)
- 컴파일·Play Mode 검증 (사용자 확인 전에는 "미검증")

**건드리지 않는 것**

- `.unity`, `.prefab`, `.asset`, `.meta` YAML 직접 편집. 사용자가 명시적으로 요청한 경우만 예외다.
- `DownloadedAssests/`, `TextMesh Pro/`, `Packages/`, `ProjectSettings/`.
- 작업 트리의 기존 미커밋 변경. 현재 `AGENTS.md`, `NEXT_WORK_CHECKLIST.md`, 기획서, `.vscode/settings.json`, `ProjectSettings/ShaderGraphSettings.asset`이 수정 상태다. 되돌리거나 덮어쓰지 않는다.
- `AGENTS.md`와 기획서 본문. 수정이 필요하면 먼저 제안하고 승인을 받는다.

**파일 형식:** C#과 문서는 기존 파일의 인코딩과 줄바꿈(CRLF)을 유지한다. 기존 `ZombieSpawner.cs`처럼 UTF-8 BOM이 있는 파일은 BOM도 유지한다. 새 `.cs` 파일을 만들면 Unity가 `.meta`를 생성하므로 `.meta`는 직접 만들지 않는다.

**작업 단위:** 한 번에 플레이 가능한 기능 하나만 구현한다. 순서는 데이터 정의 → 런타임 상태·규칙 → 에디터 연결 안내 → HUD → 검증 항목이다. 기존 동작을 바꾸는 리팩터링은 "동작 유지 단계"와 "기능 추가 단계"를 나눠서 진행한다.

## 4. 현재 코드의 실제 동작 (2026-10-06, 리뷰 보완 반영)

최신 소목표·에디터 연결·검증 증거는 `IMPLEMENTATION_REVIEW_FIXES.md`와 체크리스트의 리뷰 보완 소목표를 참고한다. 아래 구현 순서의 과거 제안보다 이미 구현·검증된 상태를 우선한다.

구현 시 이 동작과 충돌하는지 먼저 확인한다. 기획서 0장의 데이터 표와 함께 본다.

| 영역 | 동작 | 파일 |
|---|---|---|
| 웨이브 | `WavePhase`(None/Preparing/Spawning/Clearing/Completed)와 예정 생성 대기열. 일반 웨이브는 `RoundToInt(w*1.5)`를 한 프레임에 생성하고 전멸을 다음 프레임에 확정한 뒤 다음 웨이브. 10의 배수는 준비 20초 후 `ceil(B*1.5)`를 0.75초마다 최대 5마리(프레임당 1) 분할 생성. `PhaseChanged` 이벤트, 테스트용 `startWave` | `Zombie/ZombieSpawner.cs`, `Zombie/WaveStatus.cs` |
| 적 등록 | `EnemyRegistry`가 살아 있는 적·소환자별 귀속 수·생성 슬롯(예약→확정/반환)을 소유. `maxAliveEnemies` 0=무제한(기본). 웨이브 대기열 수를 받아 소환 예약보다 우선. 빈 배열 항목은 오류 로그 후 제외 | `Zombie/EnemyRegistry.cs` |
| 사망 순서 | `LivingEntity.Die()`는 `if (dead) return;` → `dead = true` → `onDeath`. 콜백 안에서 `dead`는 true | `Zombie/LivingEntity.cs` |
| 좀비 AI | Wave는 열린 완전 경로를 우선해 거점으로 이동하고, 막힌 경우 연결된 바리케이드를 공격한다. `BaseEntryRoute` 두 곳과 바리케이드 carving 장애물 사용. Idle은 정지. 8m 테스트값·벽 시야·완전 NavMesh 경로로 탐지하며 시야/거리 상실 3초→탐색 4초→역할 기본 상태 복귀. 보관소 HP 1000 테스트값, 좀비 피해만 적용, 파괴 시 패배. | `Zombie/Zombie.cs`, `BaseEntryRoute.cs`, `BaseStorage.cs`, `Barricade.cs` |
| 좀비 공격 | 트리거 + 애니메이션 이벤트(`OnAttackHit`/`OnAttackFinished`)로 타격. 타격 순간 거리·차단물 재검사. 앞을 막는 `Barricade`가 있으면 그것을 공격 | `Zombie/Zombie.cs` |
| 소환자 | `SummonerState`(Moving/Waiting/Telegraphing/Summoning/Exhausted/Dead)와 `SummonProfile`. 첫 대기 4초 → 예고 2초(슬롯 예약·표시 프리팹·이동 잠금) → 최대 2마리 → 재사용 12초, 성공 3회 후 소진. 귀속 상한 4. 위치는 2~4m 고리·Agent NavMesh·캡슐·플레이어 3m·간격·실내 제외·거점 경로·층 보정 검사. 예고 완료 시 재검증. 전부 실패 시 3초 후 재시도. 지정 대기 지점은 없음(NavMesh에 올라서면 대기) | `Zombie/CrowZombie.cs`, `Zombie/SummonProfile.cs` |
| 소음 | 플레이어 사격(30m, 1초 갱신)·이동(8m)·조준 이동(3m)·재장전(5m, 시작 1회)·아이템 획득(4m, 1회, 회복약 포함)·바리케이드 작업(10m, 작업 중 1초). `NoiseDispatcher`가 소음이 있는 프레임에만 레지스트리 목록에서 가까운 8마리를 골라 반응 요청. `HearingProfile`로 감도(Idle 1.0, AttackBase 0.5, Chase 0)·지연·오차·재반응 3초·조사·탐색. 공유 실내 구역 13개로 감쇠 적용(이동·조준·재장전·획득·작업 0.7, 총성 1.0). 소환자는 무시 | `Noise/*.cs` |
| 대기 군집 | `IdleGroupDirector`가 예정 일반 수 B 중 I(w)=min(10, floor(0.2B+0.5)) (1웨이브 0)를 에디터 지점(`IdleGroupPoint`, 플레이어 기준 25~29m 후보) 주변에 1~3마리 군집으로 배치. 추가 적이 아님. 최종 위치와 실제 생성 직전에 거리·충돌·실내·거점 경로를 검사하며 실패한 몫은 일반 생성으로 전환. 행동은 일반 Idle(8m 시야 탐지·소음 반응)과 같음. 처치·생성 30초 정체 또는 대기 적만 30초 남으면 `ForceChase`로 각성 | `Zombie/IdleGroupDirector.cs`, `Zombie/IdleGroupProfile.cs`, `Zombie/IdleGroupPoint.cs` |
| 일시정지 | `GameSessionManager`가 `Time.timeScale`을 0/1로 전환. 게임 타이머는 `Time.time`·`Time.deltaTime`·`WaitForSeconds`를 쓰면 자동으로 멈춘다. `unscaled` 시간은 현재 코드에서 쓰지 않음 | `GameSessionManager.cs` |
| 재시작 | `SceneManager.LoadScene`으로 씬을 다시 로드. 씬 객체 상태는 초기화되지만 `static` 값은 남을 수 있음 | `GameSessionManager.cs` |
| 게임오버 | `GameManager.isGameover`. 플레이어 `onDeath`에만 연결. 싱글턴은 `FindAnyObjectByType`으로 찾음 | `GameManager.cs` |
| 보급 | 2~7초 간격으로 플레이어 5m 안 NavMesh 위치에 목록 중 1개를 균등 확률로 생성하고 5초 뒤 제거. `Resources.Load`로 판자·수류탄 픽업을 추가 | `Item/ItemSpawner.cs` |
| 바리케이드 | 판자 조각 모델. 소켓마다 판자 1개(기본 20HP), 타격당 `ceil(피해/20)`조각 제거. E 길게 누름으로 1조각 설치, 재료 1개 소비. 연속 HP 감소·단계 강화·자동 설치 없음 | `Barricade.cs`, `BarricadeBuilder.cs`, `PlankInventory.cs` |
| 데이터 | `ZombieData`(체력·피해·속도·색상), `NoiseProfile`, `HearingProfile`, `SummonProfile` 에셋은 `ScriptableData/`. 웨이브 수치는 ZombieSpawner 직렬화 필드 | `ScriptableData/` |

**`AGENTS.md`와 다른 기존 코드** (해당 기능을 수정할 때만 정리하고 일괄 재작성하지 않는다)

- `ItemSpawner`의 `Resources.Load`, `BarricadeBuilder`의 0.5초마다 `FindObjectsByType`, `RandomWeaponSpawner`의 `FindAnyObjectByType` 폴백, `PlayerHealth.Awake`의 `AddComponent`.
- `Assets/Prefabs/Zombie.meta`가 손상(텍스처 임포터 내용과 잡문자열)되어 Unity가 무시하고 `Zombie 1.meta`를 따로 만들었다. `Assets/Sprites/` 일부 `.meta`도 GUID 없음. 사용자 확인 후 복구한다.

## 5. 구현 순서 (제안, 2026-10-06, 사용자 승인 전)

체크리스트 §2~§7의 의존 관계를 반영한 순서다. 사용자가 순서를 바꾸면 그 지시를 따른다.

0. **에디터 기준선 확인 (사용자 작업, 코드 없음):** 스폰 프리팹·ZombieData·스폰 지점 연결 확인.
1. **웨이브 기반 리팩터링 (동작 유지):** `EnemyRegistry`(살아 있는 적·슬롯 예약·중복 사망 방지)와 웨이브 정의 데이터를 도입하되, 물량 공식 `RoundToInt(w*1.5f)`와 "전멸하면 즉시 다음 웨이브" 동작은 그대로 둔다.
2. **좀비 AI 상태 enum + 소음 v1:** 상태 enum(`Idle`·`Alert`·`Investigate`·`Search`·`Chase`)과 소음 이벤트(사격·이동). 대기 군집·거점 공격은 제외한다.
3. **웨이브 확장:** 준비 상태, 빅웨이브(10·20·30웨이브, 물량 23/45/68), 분할 생성, 알림 이벤트.
4. **소환자 제한:** `CrowZombie`의 횟수·간격·예고·귀속/전체 상한. 슬롯 예약은 1단계의 레지스트리를 쓴다.
5. **대기 좀비 군집:** 소음 + 레지스트리 + 웨이브 정의를 결합.

바리케이드 개편, 파밍·거점 이동, 신규 특수 좀비, 도전 과제는 위 5단계의 동작을 확인한 뒤 시작한다.

**이전 결정 항목의 현재 상태 (리뷰 보완 후)**

- 탐지 8m·시야 검사·경로 검사는 테스트값으로 적용·검증했다. 밸런스 조정은 후속 작업이다.
- `LivingEntity.Die()`는 `dead = true` 후 `onDeath`를 호출하도록 이미 변경·검증했다.
- 전체 상한은 기존처럼 설정값으로 제공하고 기본 0(무제한)을 유지한다. 30마리 상한은 이전 테스트 조건이다.

## 6. 검증

`AGENTS.md` 9절과 10절의 순서와 보고 형식을 따른다. 추가로 다음을 지킨다.

- 컴파일 확인용 후보 명령(**아직 실행해 보지 않음**, Editor가 같은 프로젝트를 열고 있으면 잠금으로 실패할 수 있음):
  `"C:\Program Files\Unity\Hub\Editor\6000.3.10f1\Editor\Unity.exe" -batchmode -nographics -quit -projectPath <프로젝트 루트> -logFile <로그 경로>`
  실행하려면 먼저 사용자 확인을 받는다. 임포트로 `Library/`가 크게 바뀔 수 있다.
- **Unity CLI (사용자 허용, 2026-10-06):** 필요하면 `unity` CLI(`%LOCALAPPDATA%\Unity\bin\unity`)로 실제 동작을 확인할 수 있다. 확인된 사실:
  - `unity pipeline list`로 이 프로젝트의 Editor 실행 여부와 Pipeline 연결(Server Reachable)을 본다. 2026-10-06 사용자 승인으로 `com.unity.pipeline 0.8.0-exp.1`을 설치했다(`Packages/manifest.json` 변경). 설치 직후에는 Editor에 포커스가 가야 패키지를 임포트한다.
  - 사용 예: `unity command --no-banner --result-only <command> ...`. 명령 목록은 `unity command --detail full --query <단어>`.
  - 씬 오브젝트 대상: `--target '{"hierarchyPath":"/HUD/..."}'`. 에셋 대상: `--target "Assets/....prefab"`. 참조 값: `{"path":"Assets/....asset"}` 또는 `{"hierarchyPath":"/..."}`.
  - 프리팹·에셋 수정은 `menu --path "File/Save Project"`로 디스크에 저장된다. 씬은 `save_all`. 저장 전후 `git diff`로 의도한 필드만 바뀌었는지 확인한다.
  - Play Mode 확인은 `editor_play` → `eval --code '...'`(런타임 전용 조작·조회) → `console --since <cursor> --level error` → `editor_stop`. eval 왕복은 약 1초 이상이라 0.75초 같은 짧은 간격은 런타임에서 간격을 늘려 확인한다.
  - Editor가 이 프로젝트를 열고 있으면 batchmode 실행은 프로젝트 잠금으로 막힌다.
  - Editor 컴파일 결과는 `%LOCALAPPDATA%\Unity\Editor\Editor.log`에서 `error CS`로 확인한다. Editor가 백그라운드면 포커스를 받을 때까지 스크립트를 다시 임포트하지 않는다.
  - 대체 컴파일 확인: `Assembly-CSharp.csproj`를 scratchpad에 복사해 경로를 절대 경로로 바꾸고, ProjectReference를 `Library/ScriptAssemblies/*.dll` 참조로 바꾼 뒤 `dotnet build`. Unity 컴파일러와 같지 않으므로 "Unity 컴파일 확인"으로 보고하지 않는다.
- 수치 계산(탄약·처치 시간 등)은 현재 코드·데이터 에셋 값에 근거한다. 이전 v0.7의 80/240 HP 가정을 쓰지 않는다.
- 작업이 끝나면 `NEXT_WORK_CHECKLIST.md`의 "작업 기록"에 항목을 추가하도록 제안한다. 체크박스는 코드·에디터 연결·Play Mode 증거가 모두 있을 때만 사용자가 체크한다.
