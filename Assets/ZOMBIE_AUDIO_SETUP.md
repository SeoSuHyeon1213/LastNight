# 좀비 사운드 연결

코드의 상황별 재생은 구현했으며, 아래 클립과 이벤트는 Unity 에디터에서 연결한다. 기존 프리팹·씬의 사용자 변경은 유지한다.

## 프리팹 Inspector

기존 단일 Death Sound·Hit Sound 항목은 Inspector에서 제거했다. 이제 Death Sounds·Hurt Sounds 배열을 사용한다. 기존 연결은 숨겨진 호환 필드에 보존되며, 배열에 유효한 클립이 없을 때만 기존 클립을 사용한다.

일반 Zombie 및 BombZombie, TankZombie, DealerZombie, CrowZombie의 상속된 Sound Variations에 연결한다. 루트 AudioSource는 Play On Awake·Loop를 끄고 Spatial Blend를 1로 설정한다. 처음에는 Volume 0.5, Min Distance 2, Max Distance 20으로 조정하며 다수 생성 시 음량을 확인한다. 이 수치는 테스트 시작값이다.

`Assets/DownloadedAssests/ZombieHorrorPackageFree/WAV/VO/Zombie01` 또는 `Zombie03`에서 한 묶음씩 선택한다. 종류별 음색 배정은 직접 들어 보고 결정한다. 이 외부 사운드 에셋은 기존 Git 제외 규칙에 따라 저장소에 포함되지 않으므로 다른 PC에서도 같은 패키지를 가져와야 한다.

| Inspector 필드 | 연결할 클립 |
|---|---|
| Idle Sounds | 해당 VO 폴더의 `*_Idle_A_001~003.wav` |
| Attack Sounds | 해당 VO 폴더의 `*_Attack_A_001~003.wav` |
| Hurt Sounds | 해당 VO 폴더의 `*_Hurt_A_001~003.wav` |
| Death Sounds | `Assets/Audios/Zombie Die.wav` 등 사망 클립 지정. 배열은 선택 사항 |
| Footstep Sounds | `WAV/Footsteps/Footsteps_Zombie_concret_001~004.wav` |
| Body Fall Sounds | `WAV/BodyFall/Foley_BodyFall_001~003.wav` |

배열에서 null을 제외하고 무작위 선택하며 다른 유효 클립이 있으면 직전 클립을 다시 고르지 않는다. Hurt Sounds·Death Sounds가 비었으면 기존 Hit Sound·Death Sound를 사용한다. 대기 음성은 전투·추적 중에도 공격/능력 예고와 다른 사운드가 재생되지 않는 틈에 4~9초 간격으로 재생한다. 피격 소리는 기본 0.25초 간격으로 제한한다. 일시정지·게임 종료 중 새로운 재생은 차단하지만 이미 재생 중인 오디오를 일괄 일시정지하는 기능은 추가하지 않았다.

DealerZombie는 Windup Sound가 있으면 그것을 우선하며, 비어 있으면 Attack Sounds를 사용한다. CrowZombie의 기존 Telegraph Sound·Spawn Sound, BombZombie의 Explosion Sound는 그대로 유지한다. Bite·Impact·Eat 소리를 발소리나 대기 음성 배열에 섞지 않는다.

## 애니메이션 이벤트

- 이동 클립의 발 접지 시점: `OnFootstep`. 실제 이동 중일 때만 재생한다.
- 사망 클립의 몸이 땅에 닿는 시점: `OnBodyFall`. 사망당 한 번 재생한다.
- 공격 음성은 공격 시작 코드에서 재생하므로 같은 용도의 이벤트를 중복 연결하지 않는다.
- 기존 `OnAttackHit`·`OnAttackFinished` 이벤트를 유지한다.
- 이벤트를 받는 Animator와 Zombie 파생 컴포넌트는 같은 루트에 둔다. 외부 에셋 원본 클립을 수정하기보다 프로젝트용 클립 복사본에서 이벤트를 연결한다.

## 수동 확인

1. 한 마리와 여러 마리에서 음성 간격·종류 변경을 확인한다.
2. 연속 피격 시 소리가 과도하게 겹치지 않는지 확인한다.
3. 일반 근접 공격과 Dealer 예고 음성이 중복되지 않는지 확인한다.
4. 사망 후 대기·발소리가 멈추고 쓰러지는 소리가 한 번 재생되는지 확인한다.
5. 배열 비움·null 항목·클립 한 개에서도 기존 피격·사망 소리가 유지되는지 확인한다.
6. 비활성화·재활성화 후 사운드가 정상 재개되는지 확인한다.

Inspector 연결 및 Play Mode 청취 검증은 아직 완료 표시하지 않는다.
