// 예비 탄약은 무한이지만 탄창 소모와 재장전은 Gun의 공통 흐름을 사용한다.
public sealed class Pistol : Gun {
    public override bool HasInfiniteAmmo => true;

    public override void AddAmmo(int amount) {
        // 무한 예비 탄약은 아이템 보충이 필요하지 않다.
    }
}
