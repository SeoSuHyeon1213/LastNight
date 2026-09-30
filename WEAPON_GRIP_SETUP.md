# WeaponGrip 보강

작업 대상: G:/Game/Zombie - 복사본

1. PlayerShooter는 Grip IK Layer(기본 1, Upper Body)에서 총 정렬 후 양손 IK를 처리한다. 같은 프레임의 LateUpdate는 총을 다시 정렬하지 않고 발사한다. IK 콜백이 없을 때만 조준 fallback을 수행하므로 발사는 유지되지만 손 IK는 적용되지 않는다.
2. Gun, rifle, pistol, sniper rifle, shotgun 프리팹에 WeaponGripProfile을 추가하고 기존 Left/Right Handle을 연결했다. 기존 Handle 위치/회전은 보존한다. Position Offset은 Handle 회전 방향 기준 미터, Rotation Offset은 로컬 각도다.
3. Elbow Offset은 애니메이션 팔꿈치 위치에 캐릭터 로컬 방향으로 더한다. 초기 좌우 0.12m 바깥/0.08m 아래, 가중치 0.5는 튜닝 시작값이다.
4. 초기 Blend Seconds 0.2. 장착 시 0에서 1로, 재장전 시 Left 0 / Right 1로 전환한다. 재장전 취소·완료 후 다시 복귀한다.
5. Finger Pose는 각 Humanoid 손가락 뼈의 절대 로컬 회전이다. 배열이 비어 있으면 애니메이션을 유지한다. 프리팹 Inspector의 Finger Pose Source에 원하는 손 자세의 캐릭터 Animator를 지정하고 Capture Finger Pose로 저장한다. 캐릭터 리그가 바뀌면 다시 캡처한다. 재장전 시 포즈를 부드럽게 해제하며 비활성화·교체 시 적용 전 로컬 회전을 복원한다. Play Mode 중 인스턴스에 캡처하면 종료 시 사라지므로 원본 프리팹에 값을 저장해야 한다.

Weapon Anchor는 선택 사항이다. 플레이어 가슴/어깨 아래에 별도 Transform을 만들고 PlayerShooter에 연결하면 총의 배치 기준점으로 사용한다. Gun Pivot 자손은 사용할 수 없다. 미연결 시 애니메이션 오른쪽 팔꿈치 뼈 위치를 사용한다. 기존 GetIKHintPosition 기준과 자세 차이가 있을 수 있으므로 실제 캐릭터에서 확인한다.

검증: 전체 런타임 코드와 신규 Editor 코드 컴파일 통과, 다섯 프리팹 참조/중복 fileID 검사 통과. Unity Play Mode 실행·손가락 포즈 캡처·시각적 자세 튜닝은 미실행.
수동 확인: 정지/이동/상하 조준, 1/2와 휠 교체, 재장전 완료/도중 교체, 일시정지/복귀, 사망/재시작. 특히 팔꿈치가 꺾이거나 팔이 최대 길이로 펴지면 Handle과 Weapon Anchor 위치부터 조절한다.