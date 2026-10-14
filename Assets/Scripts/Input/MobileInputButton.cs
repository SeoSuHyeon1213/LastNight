using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

// 모바일 HUD 버튼. 누르고 있는 상태와 "눌림" 1회를 제공한다. Raycast Target이 켜진 Graphic이 필요하다.
[DisallowMultipleComponent]
public sealed class MobileInputButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler {
    private readonly HashSet<int> pointers = new HashSet<int>();
    private bool pendingPress;

    public bool IsHeld => pointers.Count > 0;

    // 눌림을 한 번만 돌려주고 지운다.
    public bool ConsumePress() {
        bool pressed = pendingPress;
        pendingPress = false;
        return pressed;
    }

    public void OnPointerDown(PointerEventData eventData) {
        if (pointers.Count == 0) pendingPress = true;
        pointers.Add(eventData.pointerId);
    }

    public void OnPointerUp(PointerEventData eventData) {
        pointers.Remove(eventData.pointerId);
    }

    // 버튼이 꺼지면 Up 이벤트가 오지 않으므로 상태를 지운다.
    private void OnDisable() {
        pointers.Clear();
        pendingPress = false;
    }
}
