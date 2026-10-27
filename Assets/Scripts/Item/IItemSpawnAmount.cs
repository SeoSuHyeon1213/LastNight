// 스포너가 생성한 인스턴스의 지급량만 설정한다. 획득 성공·소지 상한은 각 아이템이 처리한다.
public interface IItemSpawnAmount {
    void SetSpawnAmount(int amount);
}
