using UnityEngine;

// 한 프레임 동안 유지되는 플레이어 조작 입력. PlayerInput이 프레임마다 한 번 읽는다.
public struct PlayerInputFrame {
    public Vector2 move; // x: 좌우, y: 앞뒤 (-1~1)
    public bool fire; // 누르고 있는 동안
    public bool reload; // 누른 프레임
    public bool run; // 달리기 중
    public bool aim; // 조준 중
    public bool grenade; // 누른 프레임
    public bool interact; // 누른 프레임 (바리케이드 설치)
    public int weaponSlot; // 0: 요청 없음, 1·2: 해당 슬롯, -1: 다른 무기로 전환
}

// 씬별 조작 방식(키보드·마우스, 모바일 터치)을 교체하기 위한 입력 소스.
// 씬에 하나만 두거나 PlayerInput의 Source에 직접 연결한다.
public abstract class PlayerInputSource : MonoBehaviour {
    // 바리케이드 설치 안내 문구에 표시할 조작 이름
    public abstract string InteractLabel { get; }

    // HUD 상호작용 안내 문구. action은 "Install barricade"처럼 행동 이름이다.
    public virtual string FormatInteractPrompt(string action) {
        return $"Press {InteractLabel} : {action}";
    }

    // 버튼의 "누른 프레임" 입력은 읽을 때 소모된다. PlayerInput만 호출한다.
    public abstract PlayerInputFrame ReadFrame();

    // 카메라 회전량(감도 적용 전). axis 0: 좌우, 1: 상하. Cinemachine이 카메라를 갱신할 때 호출된다.
    public abstract float GetLook(int axis);
}
