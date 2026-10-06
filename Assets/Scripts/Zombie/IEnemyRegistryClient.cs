// 런타임에 생성되어 Inspector로 씬의 EnemyRegistry를 받을 수 없는 적(예: 소환자)이
// 레지스트리에 등록되는 순간 참조를 전달받기 위한 인터페이스.
public interface IEnemyRegistryClient {
    void BindRegistry(EnemyRegistry registry);
}
