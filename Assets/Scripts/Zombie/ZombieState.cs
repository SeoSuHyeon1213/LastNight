// 좀비 행동 상태. 대상이 없을 때의 기본 상태는 역할(ZombieRole)이 정한다: Wave는 AttackBase, Idle은 Idle.
public enum ZombieState {
    Idle,        // 정지 상태에서 탐지 반경 안의 대상을 찾는다
    Alert,       // 소음을 듣고 반응 지연을 기다린다
    Investigate, // 소음 추정 지점으로 이동한다
    Search,      // 소음 지점 도착 또는 추적 대상을 놓친 뒤 잠시 머문다
    Chase,       // 대상을 추적·공격한다
    AttackBase   // 보관소로 이동한다. 도착하면 공격하지 않고 Idle로 전환한다 (Wave 역할의 기본 상태)
}
