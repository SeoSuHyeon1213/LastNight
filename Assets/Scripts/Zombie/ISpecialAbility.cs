using System;

// 실행형 능력만 사용한다. 상시 장갑·약점 같은 방어 특성은 별도로 구현한다.
public interface ISpecialAbility {
    SpecialAbilityState AbilityState { get; }
    bool CanUseAbility { get; }
    int SuccessfulAbilityUses { get; }
    event Action<ISpecialAbility> AbilityExecuted;

    bool TryUseAbility();
    void CancelAbility();
}
