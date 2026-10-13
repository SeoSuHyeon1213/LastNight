using UnityEngine;

// 아이템 타입들이 반드시 구현해야하는 인터페이스
public interface IItem {
    // 입력으로 받는 target은 아이템 효과가 적용될 대상
    // 실제 효과나 수량을 얻었을 때만 true. 실패하면 필드에 남아 재진입을 기다린다.
    bool Use(GameObject target);
    void PlayPickupSound(AudioSource source);
}
