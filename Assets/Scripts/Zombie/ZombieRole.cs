// 좀비 역할. 대상이 없을 때 돌아갈 기본 상태를 정한다.
public enum ZombieRole {
    Wave, // 기본 상태 AttackBase: 보관소로 이동해 공격한다 (보관소 참조가 없으면 Idle처럼 정지)
    Idle  // 기본 상태 Idle: 정지한 채 소음과 탐지 반경으로만 반응한다 (대기 군집)
}
