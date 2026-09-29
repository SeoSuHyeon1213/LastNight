using System;
using System.Collections.Generic;
using UnityEngine;

// Input Actions 도입 전에도 실행 가능한 저장 데이터 이전 단계다.
// 실제 InputAction의 binding override 적용은 입력 초기화 담당에서 수행한다.
public static class LegacyKeyBindingMigration {
    public const string PathPrefix = "InputBindingPath.v1.";
    private const string LegacyPrefix = "KeyBinding.";
    private static readonly string[] Actions = {
        "Forward", "Backward", "Left", "Right", "Run", "Fire", "Reload", "Pause"
    };

    private static readonly Dictionary<string, string> Controls = new Dictionary<string, string> {
        { "Mouse0", "<Mouse>/leftButton" },
        { "Mouse1", "<Mouse>/rightButton" },
        { "Mouse2", "<Mouse>/middleButton" },
        { "Mouse3", "<Mouse>/forwardButton" },
        { "Mouse4", "<Mouse>/backButton" },
        { "Return", "<Keyboard>/enter" },
        { "Escape", "<Keyboard>/escape" },
        { "Space", "<Keyboard>/space" },
        { "Tab", "<Keyboard>/tab" },
        { "Backspace", "<Keyboard>/backspace" },
        { "Delete", "<Keyboard>/delete" },
        { "Insert", "<Keyboard>/insert" },
        { "Home", "<Keyboard>/home" },
        { "End", "<Keyboard>/end" },
        { "PageUp", "<Keyboard>/pageUp" },
        { "PageDown", "<Keyboard>/pageDown" },
        { "UpArrow", "<Keyboard>/upArrow" },
        { "DownArrow", "<Keyboard>/downArrow" },
        { "LeftArrow", "<Keyboard>/leftArrow" },
        { "RightArrow", "<Keyboard>/rightArrow" },
        { "LeftShift", "<Keyboard>/leftShift" },
        { "RightShift", "<Keyboard>/rightShift" },
        { "LeftControl", "<Keyboard>/leftCtrl" },
        { "RightControl", "<Keyboard>/rightCtrl" },
        { "LeftAlt", "<Keyboard>/leftAlt" },
        { "RightAlt", "<Keyboard>/rightAlt" },
        { "AltGr", "<Keyboard>/rightAlt" },
        { "LeftCommand", "<Keyboard>/leftMeta" },
        { "LeftApple", "<Keyboard>/leftMeta" },
        { "LeftWindows", "<Keyboard>/leftMeta" },
        { "RightCommand", "<Keyboard>/rightMeta" },
        { "RightApple", "<Keyboard>/rightMeta" },
        { "RightWindows", "<Keyboard>/rightMeta" },
        { "CapsLock", "<Keyboard>/capsLock" },
        { "Numlock", "<Keyboard>/numLock" },
        { "ScrollLock", "<Keyboard>/scrollLock" },
        { "Print", "<Keyboard>/printScreen" },
        { "Pause", "<Keyboard>/pause" },
        { "Menu", "<Keyboard>/contextMenu" },
        { "BackQuote", "<Keyboard>/backquote" },
        { "Minus", "<Keyboard>/minus" },
        { "Equals", "<Keyboard>/equals" },
        { "LeftBracket", "<Keyboard>/leftBracket" },
        { "RightBracket", "<Keyboard>/rightBracket" },
        { "Backslash", "<Keyboard>/backslash" },
        { "Semicolon", "<Keyboard>/semicolon" },
        { "Quote", "<Keyboard>/quote" },
        { "Comma", "<Keyboard>/comma" },
        { "Period", "<Keyboard>/period" },
        { "Slash", "<Keyboard>/slash" },
        { "KeypadEnter", "<Keyboard>/numpadEnter" },
        { "KeypadPeriod", "<Keyboard>/numpadPeriod" },
        { "KeypadDivide", "<Keyboard>/numpadDivide" },
        { "KeypadMultiply", "<Keyboard>/numpadMultiply" },
        { "KeypadMinus", "<Keyboard>/numpadMinus" },
        { "KeypadPlus", "<Keyboard>/numpadPlus" },
        { "KeypadEquals", "<Keyboard>/numpadEquals" }
    };

    public static void MigrateSavedBindings(UnityEngine.Object context) {
        bool changed = false;
        foreach (string action in Actions) {
            string destination = PathPrefix + action;
            // 항목별로 이전하므로 일부 실패가 다른 설정의 이전을 막지 않는다.
            if (PlayerPrefs.HasKey(destination) || !PlayerPrefs.HasKey(LegacyPrefix + action))
                continue;

            string legacy = PlayerPrefs.GetString(LegacyPrefix + action);
            if (TryConvert(legacy, out string path)) {
                PlayerPrefs.SetString(destination, path);
                changed = true;
            }
            else {
                Debug.LogWarning($"키 설정 이전 보류: {action} = '{legacy}'. 지원되는 경로를 확인할 때까지 원본 설정을 유지합니다.", context);
            }
        }
        if (changed) PlayerPrefs.Save();
    }

    public static bool TryConvert(string legacy, out string path) {
        path = null;
        if (string.IsNullOrEmpty(legacy)) return false;
        if (Controls.TryGetValue(legacy, out path)) return true;
        if (legacy.Length == 1 && legacy[0] >= 'A' && legacy[0] <= 'Z') {
            path = "<Keyboard>/" + legacy.ToLowerInvariant();
            return true;
        }
        if (legacy.Length == 6 && legacy.StartsWith("Alpha", StringComparison.Ordinal)
            && char.IsDigit(legacy[5]) && legacy[5] <= '9') {
            path = "<Keyboard>/" + legacy[5];
            return true;
        }
        if (legacy.Length == 7 && legacy.StartsWith("Keypad", StringComparison.Ordinal)
            && legacy[6] >= '0' && legacy[6] <= '9') {
            path = "<Keyboard>/numpad" + legacy[6];
            return true;
        }
        if (legacy.StartsWith("F", StringComparison.Ordinal)
            && int.TryParse(legacy.Substring(1), out int functionKey)
            && functionKey >= 1 && functionKey <= 12
            && legacy == "F" + functionKey) {
            path = "<Keyboard>/f" + functionKey;
            return true;
        }
        // None, Mouse5/6, 조이스틱 등을 다른 버튼으로 임의 변환하지 않는다.
        return false;
    }
}
